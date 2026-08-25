# Auditoría Independiente — Arquitectura Transaccional de Walos

> **Fecha**: Agosto 2026
> **Alcance**: Restaurante vs POS-Deli, órdenes, checkout, pagos, caja, inventario, créditos, refunds, idempotencia.
> **Método**: Lectura exhaustiva de código sin ejecución.
> **Regla**: Si algo de este informe contradice al código, manda el código.

---

## Resumen ejecutivo

El sistema tiene **dos pipelines de venta completamente divergentes** que escriben en las mismas tablas (`sales.orders`, `sales.order_items`, `sales.order_payments`, `inventory.stock`, `inventory.movements`, `sales.cash_registers`) pero con **garantías transaccionales desiguales**. El pipeline de Restaurante (`CheckoutRepository`) es maduro: tiene locking pesimista, idempotencia, replay detection, manejo de recetas, y validación de stock atómica. El pipeline POS-Deli (`PosDeliController`) es un script SQL inline en el controller con **5 vulnerabilidades transaccionales activas**.

---

## H-01 — POS-Deli puede crear stock negativo

**Severidad**: **CRÍTICA**
**Ubicación**: `PosDeliController.cs:385-391`

```sql
-- POS-Deli: no verifica que haya stock suficiente
UPDATE inventory.stock
SET quantity = quantity - @Quantity, updated_at = NOW()
WHERE company_id = @CompanyId AND branch_id = @BranchId AND product_id = @ProductId
```

vs Restaurante (`CheckoutRepository.cs:551-558`):

```sql
-- Restaurante: valida con guard clause
UPDATE inventory.stock
SET quantity = quantity - @Quantity, updated_at = NOW()
WHERE ... AND quantity >= @Quantity
RETURNING quantity
```

**Riesgo**: Cualquier venta POS-Deli puede dejar stock negativo silenciosamente. No hay validación de `AvailableQuantity >= Quantity` a nivel SQL — solo hay un check previo fuera del lock (`lines 225-233`) que tiene race condition: entre el check y el UPDATE, otra transacción puede haber decrementado el stock.

**Recomendación**: Agregar `AND quantity >= @Quantity` al UPDATE de POS-Deli y verificar `affected == 1`.

---

## H-02 — POS-Deli no tiene idempotencia

**Severidad**: **ALTA**
**Ubicación**: `PosDeliController.cs:147-451`

El endpoint `POST /pos-deli/sale` no recibe ni verifica idempotency key. Un retry de red (timeout del cliente, doble click, retry automático del browser) crea una **venta duplicada** completa: orden duplicada, stock decrementado dos veces, caja actualizada dos veces.

**Contraste**: 
- `CheckoutRepository.ProcessAsync` tiene replay detection (lines 42-49, 766-815).
- `RefundRepository.ProcessAsync` tiene `pg_advisory_xact_lock` + idempotency key + fingerprint (lines 193-230).

**Recomendación**: Agregar header `Idempotency-Key` al endpoint POS-Deli. Usar `pg_advisory_xact_lock` o INSERT con ON CONFLICT como ya hace RefundRepository.

---

## H-03 — POS-Deli crea "mesas fantasma"

**Severidad**: **MEDIA**
**Ubicación**: `PosDeliController.cs:261-273`

```sql
INSERT INTO sales.tables (... table_number, name, status ...)
VALUES (... @TableNumber, 'POS DELI POS-...', 'invoiced' ...)
```

Cada venta POS-Deli crea un registro en `sales.tables` con `table_number` aleatorio (900000-999999) y status directo `'invoiced'`. Esto contamina:

- `GET /sales/tables` (muestra mesas activas — estas no aparecen porque están invoiced, pero sí en histórico).
- Reportes y queries que hacen `JOIN sales.tables` o `COUNT(*) FROM sales.tables`.
- Búsquedas de órdenes que muestran "POS DELI POS-20260801..." como nombre de mesa.

**Recomendación**: O bien crear la tabla con un flag `is_pos_quick_sale = true` para filtrar, o mejor: permitir que `sales.orders.table_id` sea nullable para POS-Deli y no crear tabla fantasma.

---

## H-04 — Movimientos de inventario POS-Deli sin trazabilidad completa

**Severidad**: **MEDIA**
**Ubicación**: `PosDeliController.cs:393-422`

```sql
-- POS-Deli: falta reference_type, reference_id, stock_after
INSERT INTO inventory.movements (
    company_id, branch_id, product_id, movement_type, quantity,
    unit_cost, notes, created_by, created_at
) VALUES (...)
```

vs Restaurante (`CheckoutRepository.cs:565-587`):

```sql
-- Restaurante: tiene trazabilidad completa
INSERT INTO inventory.movements (
    ... reference_type, reference_id, stock_after, ...
) VALUES (
    ... 'order', @OrderId, @StockAfter, ...
)
```

**Impacto**: Los movimientos POS-Deli no tienen `reference_type='order'`, `reference_id=orderId` ni `stock_after`. Esto rompe:
- Auditoría de inventario: no se puede trazar un movimiento a su orden.
- Reportes de stock: no se sabe el stock resultante después del movimiento.
- Conciliación: si hay discrepancia de stock, no se puede rastrear la causa.

---

## H-05 — POS-Deli no maneja productos "prepared" (recetas)

**Severidad**: **MEDIA**
**Ubicación**: `PosDeliController.cs:235-251` (normalizedItems) — no filtra por `product_type`

El Restaurante (`CheckoutRepository.BuildInventoryMovementsAsync`, lines 436-512) tiene lógica para:
1. Filtrar items `prepared` del descuento directo de stock.
2. Buscar recetas (ingredientes) del producto preparado.
3. Descontar ingredientes proporcionales.

POS-Deli descuenta stock del producto directamente, sin verificar si es `prepared`. Si una salsamentaría vende un producto tipo "Bandeja mixta" que es `product_type='prepared'` con receta:
- El stock del producto preparado se descuenta (incorrecto — no tiene stock propio).
- Los ingredientes **no** se descuentan (las materias primas no bajan).

**Impacto adicional en refund**: Si se refunde una orden POS-Deli con producto prepared, `RefundRepository.RestoreInventoryAsync` intentará restaurar ingredientes que nunca fueron consumidos → stock inflado.

---

## H-06 — Lógica de actualización de caja escrita 3 veces

**Severidad**: **MEDIA** (mantenibilidad + divergencia)

La lógica de `UPDATE sales.cash_registers SET total_X = total_X + @X` existe en 3 lugares con **campos diferentes**:

| Ubicación | total_sales | cash | card | transfer | other | discounts | credits | tips | order_count |
|---|---|---|---|---|---|---|---|---|---|
| `CheckoutRepository:630-661` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `PosDeliController:355-379` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ |
| `CashRegisterService:157-177` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

POS-Deli **no actualiza** `total_discounts`, `total_credits`, `total_tips`. Aunque POS-Deli no soporta descuentos/créditos/propinas hoy, si en el futuro los agrega, el resumen de caja estará desbalanceado.

Además, `CashRegisterService.UpdateTotalsFromOrderAsync` **no se usa** en ningún flujo activo — es dead code potencial o un método preparado para un tercer flujo que nunca se conectó.

---

## H-07 — No hay forma de distinguir Restaurant vs POS-Deli en la DB

**Severidad**: **MEDIA**

No existe columna `order_type` en `sales.orders`. La única diferencia es el prefijo del `order_number`:
- Restaurante: `ORD-{tableId}-{timestamp}`
- POS-Deli: `POS-{timestamp}-{random}`

**Riesgo**:
- Reportes de ventas mezclan ambos flujos sin poder filtrar.
- Un dashboard de "ventas del restaurante" incluye silenciosamente ventas POS-Deli.
- Si algún otro módulo genera orders con otro prefijo, la distinción se rompe.

**Recomendación**: Agregar columna `order_type TEXT DEFAULT 'restaurant'` a `sales.orders`. POS-Deli setea `'pos-deli'`. Todos los reportes pueden filtrar por tipo.

---

## H-08 — CheckoutRepository es un "service disfrazado de repository"

**Severidad**: **MEDIA** (arquitectural)

`CheckoutRepository` (1020 líneas) contiene:
- Reglas de negocio de descuentos (líneas 320-420): política de descuento, umbrales, override.
- Cálculos financieros: net total, credit amount, split count.
- Orquestación transaccional: 9 pasos secuenciales con locks.
- Replay/idempotencia.

Esto viola Clean Architecture: un repository no debería contener reglas de negocio. Pero **moverlo a Application layer sin cuidado rompe atomicidad** (ver H-12).

**Recomendación**: Renombrar a `CheckoutProcessor` o `CheckoutUseCase` y moverlo a Application. Mantener la transacción inyectada.

---

## H-09 — POS-Deli: toda la lógica está en el controller

**Severidad**: **MEDIA** (arquitectural)

`PosDeliController.CreateSale` (300+ líneas de SQL inline):
- Validación de request
- Lectura y validación de productos
- Creación de tabla fantasma
- Creación de orden
- Inserción de items
- Inserción de pagos
- Actualización de caja
- Descuento de stock
- Registro de movimientos

Todo en un solo método del controller. No hay service layer, no hay repository.

**Impacto**: Imposible de testear unitariamente. Imposible de reutilizar. Cualquier cambio en la lógica de venta requiere tocar el controller HTTP.

---

## H-10 — Refund: locking robusto pero acoplado a tablas reales

**Severidad**: **BAJA** (riesgo latente)

`RefundRepository.ProcessAsync` es el código más maduro del sistema:
- `pg_advisory_xact_lock` para idempotencia.
- Idempotency key + fingerprint para replay.
- Manejo de créditos (reducción proporcional).
- Restauración de stock con `ON CONFLICT`.
- Cálculo de porción cash proporcional.
- Movimientos de caja.

Pero asume que la orden viene del flujo Restaurante. Si se refunde una orden POS-Deli:
- ✅ Funciona: encuentra la orden, los items, los payments.
- ⚠️ Mesas fantasma: buscará el table y lo encontrará (existe aunque sea fantasma).
- ❌ Recetas: si hay productos `prepared`, intentará restaurar ingredientes que POS-Deli nunca consumió.

---

## H-11 — CreditService.AddPaymentAsync fuera de transacción de caja

**Severidad**: **BAJA** (riesgo de desincronización)

`CreditService.AddPaymentAsync` delega a `_creditRepo.ProcessPaymentAsync` que procesa el pago del crédito. Pero la actualización de la caja registradora con el abono **no está en la misma transacción**.

Si el pago de crédito se registra pero la caja no se actualiza (o viceversa), los totales de caja quedan desbalanceados.

**Contraste**: Checkout y Refund sí actualizan la caja dentro de la misma transacción.

---

## H-12 — Riesgo de romper atomicidad en una unificación ingenua

**Severidad**: **ALTA** (riesgo de diseño)

Si se intenta unificar Restaurante y POS-Deli extrayendo "servicios" separados:

```
❌ PELIGROSO:
CheckoutService
  → PaymentService.InsertPayments()      ← abre su propia conexión
  → InventoryService.DecrementStock()    ← abre su propia conexión
  → CashRegisterService.UpdateTotals()   ← abre su propia conexión
```

Cada service abre su propio `IDbConnection`, lo que significa transacciones separadas. Si `DecrementStock` falla después de `InsertPayments`, los pagos quedan huérfanos.

**La razón por la que CheckoutRepository funciona hoy** es que pasa `IDbConnection` + `IDbTransaction` como parámetros a métodos privados estáticos — todo corre en la misma transacción.

**Recomendación para unificación segura**:

```
✅ CORRECTO:
CheckoutUseCase(IDbConnectionFactory)
  → abre UNA conexión + UNA transacción
  → llama métodos internos pasando (connection, transaction)
  → PaymentOperations.Insert(connection, transaction, ...)
  → InventoryOperations.Decrement(connection, transaction, ...)
  → CashRegisterOperations.Update(connection, transaction, ...)
  → COMMIT o ROLLBACK
```

---

## Hotspots de concurrencia

| Recurso | Lock | Duración | Impacto |
|---|---|---|---|
| `sales.cash_registers` row | `FOR UPDATE` en Checkout y Refund; row-level en POS-Deli | Toda la transacción (~50-200ms) | Serializa TODAS las ventas del mismo cajero. Aceptable si un cajero no hace >5 ventas/segundo |
| `inventory.stock` row por producto | `FOR UPDATE` en Checkout; sin lock en POS-Deli | Toda la transacción | Serializa ventas concurrentes del mismo producto. Riesgo en salsamentaría: si dos cajeros venden el mismo jamón simultáneamente, POS-Deli puede oversell |
| `sales.tables` row | `FOR UPDATE` en Checkout | Toda la transacción | Solo Restaurante. Previene doble facturación de la misma mesa |
| `pg_advisory_xact_lock` | Advisory lock en Refund | Toda la transacción | Serializa refunds con misma idempotency key. Correcto |

---

## Mapa de responsabilidades actual vs recomendado

### Estado actual

```
SalesController
  └─ SalesService (576 líneas)
       ├─ Table CRUD, items, queries, receipts, CSV export
       └─ InvoiceTableAsync → CheckoutRepository.ProcessAsync (1020 líneas)
                                ├─ Locking (table, order, items, register, stock)
                                ├─ Reglas de descuento
                                ├─ Cálculos financieros
                                ├─ Inventory movements + recipe expansion
                                ├─ Payment insertion
                                ├─ Cash register update
                                ├─ Credit creation
                                ├─ Replay detection
                                └─ Order/Table completion

PosDeliController (464 líneas)
  └─ CreateSale (inline, sin service)
       ├─ Validación
       ├─ Cash register check
       ├─ Product lookup + stock check
       ├─ Table/Order/Items/Payments INSERT
       ├─ Cash register UPDATE (parcial)
       ├─ Stock UPDATE (sin guard)
       └─ Movement INSERT (sin trazabilidad)

RefundController
  └─ RefundService
       └─ RefundRepository.ProcessAsync (820 líneas)
            ├─ pg_advisory_xact_lock + idempotency
            ├─ Item selection + net refund calculation
            ├─ Credit adjustment
            ├─ Cash portion calculation
            ├─ Stock restoration + recipe restoration
            └─ Cash register movement

CreditController
  └─ CreditService
       └─ CreditRepository.ProcessPaymentAsync
            └─ (fuera de transacción de caja)

CashRegisterController
  └─ CashRegisterService
       └─ Open/Close/Movement/Summary
```

### Recomendación de responsabilidades

```
OrderService
  ├─ CreateOrder (restaurant o pos-deli)
  ├─ AddItems / UpdateItemQuantity
  ├─ CancelOrder
  ├─ GetOrderDetail / SearchOrders / GetReceipt
  └─ NO toca pagos, stock, ni caja

CheckoutService (orquestador transaccional)
  ├─ Entry point: ProcessCheckout(CheckoutCommand)
  │   └─ CheckoutCommand tiene un OrderType (restaurant | pos-deli)
  ├─ Abre UNA conexión + transacción
  ├─ Llama a:
  │   ├─ OrderOperations.Lock + Validate
  │   ├─ DiscountCalculator.Calculate (solo si aplica)
  │   ├─ PaymentOperations.Insert
  │   ├─ InventoryOperations.Decrement (con recipe expansion)
  │   ├─ CashRegisterOperations.Update
  │   └─ CreditOperations.Insert (solo si aplica)
  └─ COMMIT o ROLLBACK

PaymentOperations (estático, recibe connection+transaction)
  ├─ InsertPayments
  ├─ ValidatePaymentMethods
  └─ ResolvePaymentMethod

InventoryOperations (estático, recibe connection+transaction)
  ├─ LockAndValidateStock
  ├─ DecrementStock (con guard quantity >= @Quantity)
  ├─ ExpandRecipes
  ├─ CreateMovements (con reference_type, reference_id, stock_after)
  └─ RestoreStock (para refunds)

CashRegisterOperations (estático, recibe connection+transaction)
  ├─ LockActiveRegister
  ├─ IncrementTotals (ALL fields)
  └─ RegisterMovement

RefundService (mantener, ya está bien)
  └─ Ajustar para usar InventoryOperations.RestoreStock
     y CashRegisterOperations.RegisterMovement

CreditService (mantener, pero meter pago de crédito en transacción de caja)
```

---

## Priorización de remediación

### P0 — Arreglar antes de producción POS-Deli

| # | Hallazgo | Esfuerzo |
|---|---|---|
| 1 | **H-01**: Stock negativo — agregar `AND quantity >= @Quantity` + check `affected==1` | 15 min |
| 2 | **H-02**: Idempotencia — agregar `Idempotency-Key` header + `pg_advisory_xact_lock` | 2 h |
| 3 | **H-04**: Trazabilidad — agregar `reference_type`, `reference_id`, `stock_after` a movements | 30 min |

### P1 — Antes de la siguiente feature de ventas

| # | Hallazgo | Esfuerzo |
|---|---|---|
| 4 | **H-07**: Agregar columna `order_type` a `sales.orders` | 1 h |
| 5 | **H-03**: Eliminar mesas fantasma — hacer `table_id` nullable o usar flag | 2 h |
| 6 | **H-05**: Manejar productos `prepared` en POS-Deli | 3 h |
| 7 | **H-06**: Extraer lógica de cash register update a un solo lugar | 2 h |

### P2 — Deuda arquitectónica (antes de unificación)

| # | Hallazgo | Esfuerzo |
|---|---|---|
| 8 | **H-09**: Extraer lógica de PosDeliController a service/processor | 4 h |
| 9 | **H-08**: Renombrar/mover CheckoutRepository a Application layer | 3 h |
| 10 | **H-12**: Diseñar la unificación con transacción compartida | 8 h |
| 11 | **H-11**: Meter pago de crédito en transacción de caja | 2 h |

---

## Veredicto sobre unificación

**¿Se puede unificar?** Sí, pero con cuidado.

**¿Se debe unificar ahora?** No. Primero corregir P0 (stock negativo + idempotencia) para que POS-Deli sea seguro en producción. La unificación es un refactor P2 que requiere diseño previo.

**Patrón de unificación recomendado**:
1. Extraer las 5 "operaciones" compartidas como clases estáticas con firma `(IDbConnection, IDbTransaction, ...)`.
2. Crear `CheckoutService` en Application layer que orqueste la transacción.
3. Parametrizar con `OrderType` las diferencias (mesa, descuento, propina, crédito, recetas).
4. Mantener dos entry points en controllers pero delegando ambos al mismo `CheckoutService`.
5. **Nunca** abrir conexiones separadas dentro de la transacción.
