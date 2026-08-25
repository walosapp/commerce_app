# Revisión Independiente — Bloque 1.1 de Fase 1

> **Línea base**: `phase-0-deploy-ready / 1134a83`
> **Fecha**: Agosto 2026
> **Método**: Lectura exhaustiva de diff + archivos nuevos + tests. Sin ejecución.
> **Suite reportada**: 460 PASS / 0 FAIL / 0 SKIP

---

## 1. Pureza de las policies

### PaymentPolicy — OK

**Ubicación**: `Walos.Domain.Policies.PaymentPolicy`
**Dependencias**: `System.Collections.Frozen`, `Walos.Domain.Exceptions` — ambas domain-only.
**Veredicto**: **OK** — estática pura, sin I/O, sin inyección, sin dependencias de infraestructura.

### SaleItemPolicy — OK

**Ubicación**: `Walos.Domain.Policies.SaleItemPolicy`
**Dependencias**: `Walos.Domain.Exceptions`, referencia a `PaymentPolicy.RoundMoney` (mismo namespace domain).
**Veredicto**: **OK** — estática pura. `CreateSnapshot` produce un record inmutable calculado server-side.

### InventoryMovementPlanner — OK

**Ubicación**: `Walos.Domain.Policies.InventoryMovementPlanner`
**Dependencias**: `Walos.Domain.Exceptions`, referencia a `PaymentPolicy.RoundMoney`.
**Veredicto**: **OK** — estática pura. Produce `InventoryMovementPlan` records. No hace persistencia, locks, ni abre conexiones. `WithStockAfter` retorna un nuevo record inmutable (`with`).

---

## 2. PaymentPolicy — categorías contables y nequi

### Categorías contables canónicas — OK

```csharp
CanonicalMethods = { "cash", "card", "transfer", "other" }
```

Test `Canonical_Accounting_Methods_Do_Not_Treat_Nequi_As_Category` confirma el set exacto y que `ToAccountingMethod("nequi") == "transfer"`.

### Nequi como legacy aceptado — OK

```csharp
AcceptedMethods = { "cash", "card", "transfer", "other", "nequi" }
```

`NormalizeMethod("NeQuI")` devuelve `"nequi"` (se persiste tal cual).
`ToAccountingMethod("nequi")` devuelve `"transfer"` (contablemente es transferencia).

### Rechazo de métodos desconocidos — OK

`NormalizeMethod("crypto")` lanza `ValidationException`. Test `Unknown_Method_Is_Rejected_Instead_Of_Becoming_Other` lo cubre.
En la línea base, POS-Deli calculaba `totalOtherSales` con un `!Contains(["cash","card","transfer","nequi"])` que habría clasificado "crypto" como "other" silenciosamente. Ahora se rechaza antes.

---

## 3. Nequi — semántica de flujos anteriores

### Checkout Restaurante — OK

`CheckoutRepository.UpdateCashRegisterAsync` (líneas 615-665): Ahora usa `PaymentPolicy.ToAccountingMethod()` para clasificar. Antes tenía `payment.Method is "transfer" or "nequi"` inline. **El resultado contable es idéntico**: nequi suma a `total_transfer_sales`.

`PaymentsEqual` en replay (línea 831) usa `NormalizePaymentMethod` local (lowercase trim). Un checkout guardado con `"nequi"` se compara como `"nequi"` contra el request replay. **Replay sigue funcionando** porque los pagos se persisten como `"nequi"` y la comparación es string-exact, no contable.

### POS-Deli — OK

Misma mecánica: `ToAccountingMethod` para caja, método original persistido en `order_payments`.
Test `CreateSale_Nequi_Remains_Persisted_But_Is_Accounted_As_Transfer` verifica:
- `order_payments.method = "nequi"` (persistido)
- `cash_registers.total_transfer_sales = 100` (contable)
- `cash_registers.total_other_sales = 0` (no se filtra a other)

### Refund — OK

`RefundRepository.CalculateCashPortionAsync` (líneas 531-537): Ahora usa `PaymentPolicy.IsAcceptedMethod` + `NormalizeMethod`. El filtro de cash sigue siendo `payment.Method == "cash"` (línea 544). Un pago original `"nequi"` normalizado a `"nequi"` **no** se cuenta como cash → no genera `cash_out`. **Correcto**: nequi no modifica efectivo físico.

### Credit payment — OK

`CreditService.AddPaymentAsync`: Ahora usa `PaymentPolicy.NormalizeMethod`. `CreditRepository.ProcessPaymentAsync` (línea 197) solo activa `cash_in` cuando `command.PaymentMethod == "cash"`. Nequi normalizado a `"nequi"` → no activa `cash_in`. **Correcto**.

---

## 4. Cambios de rounding y órdenes históricas

### Rounding unificado — OK con observación

Antes: `Math.Round(x, 2)` (default `ToEven` / banker's rounding).
Ahora: `PaymentPolicy.RoundMoney(x)` = `Math.Round(x, 2, MidpointRounding.AwayFromZero)`.

**¿Puede alterar órdenes históricas?** No. Las policies solo se ejecutan en flujos de escritura nuevos (checkout, add items, create table). No re-calculan datos ya persistidos.

**¿Puede alterar refunds de órdenes históricas?** `RefundRepository` no fue migrado a `PaymentPolicy.RoundMoney` — sigue usando `Math.Round(x, 2)` inline (verificado: 10 ocurrencias de `Math.Round` en RefundRepository, ninguna cambiada a `PaymentPolicy.RoundMoney`). **Esto es correcto**: los refunds operan sobre montos ya persistidos y deben usar el rounding original.

### Observación MEDIUM sobre rounding mixto

| Componente | Rounding monetario |
|---|---|
| CheckoutRepository (nuevo checkout) | `AwayFromZero` via `PaymentPolicy.RoundMoney` |
| RefundRepository (refund de esos checkouts) | `ToEven` via `Math.Round(x, 2)` |
| CreditRepository (credit adjustments) | `Math.Round(x, 2)` — `ToEven` |

La diferencia solo se manifiesta en valores que caen exactamente en `.XX5`. Ejemplo: un subtotal de `10.015` se redondea a `10.02` en checkout (AwayFromZero) pero un refund parcial que recalcule podría redondear a `10.01` (ToEven). La diferencia máxima es `0.01`, dentro de la tolerancia. Pero es una **divergencia semántica latente** que debería unificarse en un bloque futuro.

**Veredicto**: **MEDIUM** — no es regresión del Bloque 1.1 porque RefundRepository no fue tocado en esta dimensión, pero queda como deuda.

---

## 5. Tolerancia 0.01 — consistencia

### CheckoutRepository — OK

`PaymentPolicy.ValidatePaymentTotal` usado en `Calculate()` (línea ~403). Tolerancia: `ReconciliationTolerance = 0.01m`.

### POS-Deli — OK

`PaymentPolicy.ValidatePaymentTotal` usado en `CreateSale` (línea ~262). Misma constante.

### Antes (línea base)

- Restaurante: `Math.Abs(sum - expected) > 0.01m` — misma tolerancia.
- POS-Deli: `Math.Abs(paymentTotal - total) > 1` — **tolerancia era de $1, no $0.01**.

**Esto es un cambio de comportamiento**: POS-Deli pasó de tolerar $1 de diferencia a $0.01. Es un endurecimiento intencional y correcto. Test `CreateSale_Payment_Difference_Of_One_Cent_Is_Accepted` y `CreateSale_Payment_Difference_Above_One_Cent_Is_Rejected_With_Rollback` lo cubren.

**Veredicto**: **OK** — cambio intencional, bien cubierto por tests.

---

## 6. SaleItemPolicy — snapshots controlados por frontend

### CreateSnapshot — OK

`SaleItemPolicy.CreateSnapshot(productId, productName, quantity, unitPrice)`:
- `unitPrice` se recibe del **server** (producto de la DB), no del request.
- En `SalesService.PrepareOrderItemsAsync` (línea ~533-545): `product.SalePrice` viene de `_inventoryRepo.GetProductByIdAsync`.
- En `PosDeliController` (línea ~243-258): `(decimal)product.saleprice` viene del SQL `SELECT sale_price FROM inventory.products`.
- El frontend envía `ProductId` y `Quantity`, no `UnitPrice`.

**Veredicto**: **OK** — el snapshot toma el precio del server. El frontend no puede inyectar un precio custom.

---

## 7. Rechazo de más de 2 decimales vs NUMERIC(18,2)

### Policy — OK

```csharp
CurrentQuantityDecimals = 2;
IsQuantitySupported(q) => q > 0 && Round(q, 2, AwayFromZero) == q;
```

Test `Three_Decimal_Quantity_Is_Temporarily_Rejected_Until_Order_Items_Migrate` verifica que `1.255m` se rechaza, y `TargetQuantityDecimals = 3` documenta la intención futura.

### Coherencia con la DB — OK

Test de integración `Database_Currently_Rounds_OrderItem_Quantity_1255_To_126` demuestra que la DB silenciosamente redondea `1.255` → `1.26` y rompe el subtotal (`126` vs orden `125.50`). El rechazo en la policy previene esta corrupción.

### Aplicación en ambos flujos — OK

- **Restaurante**: `SalesService.ValidateItemsAsync` (línea ~500) y `SalesService.PrepareOrderItemsAsync` (línea ~519) llaman `SaleItemPolicy.NormalizeQuantity`. `CheckoutRepository.AddItemsAsync` (línea ~91) también. `UpdateItemQuantityAsync` en SalesService (línea ~212) y CheckoutRepository (línea ~170) también.
- **POS-Deli**: `PosDeliController` (línea 158) usa `SaleItemPolicy.IsQuantitySupported` en la validación previa. **Nota**: usa `IsQuantitySupported` (retorna bool) en vez de `NormalizeQuantity` (lanza excepción) porque el controller retorna `BadRequest` en vez de lanzar.

**Veredicto**: **OK** — coherente.

---

## 8. InventoryMovementPlanner — responsabilidad

### Análisis — OK

`InventoryMovementPlanner.Create` devuelve un `InventoryMovementPlan` record inmutable. No hace:
- ❌ Apertura de conexión
- ❌ Queries SQL
- ❌ Locks (`FOR UPDATE`)
- ❌ Transacciones
- ❌ Persistencia (`INSERT`/`UPDATE`)

Es puramente un **factory de DTOs validados**.

`WithStockAfter` retorna un nuevo record con `plan with { StockAfter = stockAfter }` — pattern inmutable.

**Nota**: `InventoryMovementPlanner` no está siendo *usado* todavía por CheckoutRepository ni PosDeliController. Es código preparatorio. CheckoutRepository mantiene su `InventoryMovementPlan` privado interno (clase, no record, distinta del policy). La integración real ocurrirá en bloques posteriores.

**Veredicto**: **OK** — puro, sin acoplamiento. No interfiere con nada existente.

---

## 9. CheckoutRepository.ProcessAsync — conexión, transacción, orden de locks

### Verificación — OK

```csharp
using var connection = await _connectionFactory.CreateConnectionAsync();  // UNA conexión
using var transaction = connection.BeginTransaction();                    // UNA transacción
```

Orden de locks (sin cambios respecto a línea base):
1. `LockTableAsync` — `FOR UPDATE` en `sales.tables`
2. `LockOrderAsync` — `FOR UPDATE` en `sales.orders`
3. `LoadItemsAsync` — `FOR UPDATE OF oi` en `sales.order_items`
4. `LockActiveRegisterAsync` — `FOR UPDATE` en `sales.cash_registers`
5. `ApplyInventoryAsync` — `FOR UPDATE` en `inventory.stock`

Commit al final, rollback en catch. **Idéntico a la línea base**.

Los cambios del Bloque 1.1 en CheckoutRepository son exclusivamente:
- Reemplazo de `Math.Round(x, 2)` → `PaymentPolicy.RoundMoney(x)`
- Reemplazo de validation inline → `PaymentPolicy.NormalizeMethod` / `NormalizePositiveAmount`
- Reemplazo de `AllowedPaymentMethods` HashSet → `PaymentPolicy.AcceptedMethods`
- Reemplazo de `ResolvePaymentMethod` local → `PaymentPolicy.ResolvePersistedMethod`
- Reemplazo de nequi/transfer inline → `PaymentPolicy.ToAccountingMethod`
- `SaleItemPolicy.NormalizeQuantity` y `IsQuantitySupported` para validación de quantities

**Ningún cambio estructural, de flujo, ni de locks.**

**Veredicto**: **OK**

---

## 10. Credit y Refund — semántica cash_in / cash_out / nequi

### Credit payment (abono efectivo → cash_in) — OK

`CreditRepository.ProcessPaymentAsync` línea 197: `if (command.PaymentMethod == "cash")` → lock register → `cash_in + @Amount` + movement type `'in'`.
`CreditService.AddPaymentAsync`: normaliza con `PaymentPolicy.NormalizeMethod`. Un pago nequi se normaliza a `"nequi"` → no dispara `cash_in`. **Correcto**.

### Refund (refund efectivo → cash_out) — OK

`RefundRepository.CalculateCashPortionAsync` línea 543-544: filtra `payment.Method == "cash"`. Solo cash puro genera `cashToReturn > 0`.
`RegisterCashRefundAsync`: `cash_out = cash_out + @Amount` + movement type `'out'`.
Nequi normalizado como `"nequi"` no es `"cash"` → no genera `cash_out`. **Correcto**.

### Resumen de flujo de caja

| Evento | cash | nequi | card | transfer |
|---|---|---|---|---|
| Venta → caja | `total_cash_sales += X` | `total_transfer_sales += X` | `total_card_sales += X` | `total_transfer_sales += X` |
| Abono crédito | `cash_in += X` | nada | nada | nada |
| Refund | `cash_out += X` | nada | nada | nada |

**Veredicto**: **OK** — semántica preservada sin cambios.

---

## 11. Regresiones no cubiertas por tests nuevos

### R-01: `NormalizePaymentMethod` residual en `PaymentsEqual` — LOW

`CheckoutRepository.PaymentsEqual` (línea 831) usa `NormalizePaymentMethod` (el método privado local, lowercase-trim) para comparar pagos en replay. El Bloque 1.1 eliminó `AllowedPaymentMethods` pero dejó `NormalizePaymentMethod` como método privado.

No es una regresión funcional: los pagos entran ya normalizados por `PaymentPolicy.NormalizeMethod` antes de persistirse, así que al comparar replay ambos strings ya son lowercase-trimmed. Pero es **dead-logic parcial** — `NormalizePaymentMethod` podría eliminarse en favor de `PaymentPolicy.NormalizeMethod` en el replay path.

**Veredicto**: **LOW** — no afecta comportamiento, es limpieza pendiente.

### R-02: RefundRepository rounding no unificado — MEDIUM

Documentado en sección 4. RefundRepository mantiene `Math.Round(x, 2)` (ToEven). Si un checkout nuevo usa AwayFromZero y produce un total de `.XX5`, un refund posterior podría diferir en ±0.01. Dentro de tolerancia pero semánticamente inconsistente.

No hay test que cubra un refund de una orden creada con el nuevo rounding.

**Veredicto**: **MEDIUM** — deuda aceptable si se agenda para un bloque futuro.

### R-03: `SaleItemPolicy.CalculateSubtotal` usa dummy snapshot — LOW

```csharp
public static decimal CalculateSubtotal(decimal quantity, decimal unitPrice) =>
    CreateSnapshot(1, "snapshot", quantity, unitPrice).Subtotal;
```

Crea un snapshot con `productId=1` y `productName="snapshot"` como valores dummy para reutilizar la lógica. Funciona, pero si `CreateSnapshot` agrega validaciones futuras sobre productId/name, esto podría romperse silenciosamente.

**Veredicto**: **LOW** — no es regresión, es fragilidad menor.

---

## 12. Calidad de tests de caracterización

### Tests que prueban **comportamiento** (correctos)

- `CreateSale_Nequi_Remains_Persisted_But_Is_Accounted_As_Transfer` — verifica resultado observable en DB
- `CreateSale_Payment_Difference_Of_One_Cent_Is_Accepted` — verifica boundary behavior
- `CreateSale_Payment_Difference_Above_One_Cent_Is_Rejected_With_Rollback` — verifica rollback
- `CreateSale_WeightedDecimal_PersistsEconomics_StockPayment_AndCashRegister` — end-to-end
- `CreateSale_Concurrent_Sales_Do_Not_Persist_Negative_Stock` — concurrencia real
- `Database_Currently_Rounds_OrderItem_Quantity_1255_To_126` — documenta comportamiento de DB

### Tests que podrían estar probando implementación — ninguno detectado

Los tests de `PaymentPolicyTests` y `SaleItemPolicyTests` prueban contratos públicos de las policies (inputs → outputs / excepciones). No mockean internals ni verifican llamadas. Son **tests de contrato**, no de implementación.

`InventoryMovementPlannerTests` verifica que el record producido tiene los campos correctos. Es un **test de factory**, apropiado para un value object.

### Tests de caracterización de bugs pendientes — OK, bien documentados

Cada test documenta explícitamente que es comportamiento actual pendiente de corrección:

| Test | Bug documentado | Bloque destino |
|---|---|---|
| `CreateSale_CurrentBehavior_Allows_Inactive_Product_Pending_Block_12` | Producto inactivo aceptado | 1.2 |
| `CreateSale_CurrentBehavior_Allows_NotForSale_Product_Pending_Block_12` | Producto no vendible aceptado | 1.2 |
| `CreateSale_CurrentBehavior_Does_Not_Consume_Prepared_Recipe_Pending_Block_12` | Recipe no consumida | 1.2 |
| `CreateSale_CurrentBehavior_Allows_Prepared_Without_Recipe_Pending_Block_12` | Prepared sin receta aceptado | 1.2 |
| `CreateSale_Movement_Uses_Real_Cost_But_Traceability_Remains_Pending_Block_12` | Sin reference_type/stock_after | 1.2 |
| `CreateSale_CurrentBehavior_Ignores_Stock_Committed_By_Open_Table_Pending_Block_12` | Stock comprometido ignorado | 1.2 |
| `CreateSale_CurrentBehavior_Retry_Creates_A_Second_Sale_Pending_Block_13` | Sin idempotencia | 1.3 |
| `CreateSale_CurrentBehavior_Different_Retry_Payload_Creates_Another_Sale_Pending_Block_13` | Sin idempotencia | 1.3 |

**Veredicto**: **OK** — todos usan `Assert.IsType<OkObjectResult>` (no excepción) confirmando que el bug *existe*. Cuando el Bloque 1.2/1.3 corrija, estos tests deben cambiar a `Assert.Throws` o `Assert.IsType<BadRequestObjectResult>`.

---

## 13. Bugs deliberadamente pendientes — verificación de no-interferencia

### POS permite producto inactivo/no vendible — OK, sin arreglo parcial

POS-Deli sigue sin verificar `is_active` ni `is_for_sale` en su query de productos para venta. El SELECT (líneas 224-233 del controller) no filtra por estos campos. Tests de caracterización lo confirman.

### Prepared no consume receta — OK, sin arreglo parcial

POS-Deli no tiene lógica de recipe expansion. El loop de stock (línea 405) descuenta `item.Quantity` directamente sin verificar `product_type`. Sin cambios.

### Prepared sin receta se puede vender — OK, sin arreglo parcial

`SaleItemPolicy.ValidatePreparedRecipe` existe como policy pero **no está invocado** por PosDeliController ni por CheckoutRepository en este bloque. Es código preparatorio. Test de caracterización confirma que POS acepta prepared sin receta.

**Nota positiva**: la policy existe lista para usar en Bloque 1.2 sin necesidad de redesign.

### Stock comprometido no considerado — OK, sin arreglo parcial

POS-Deli solo verifica stock disponible, no resta orders pending de restaurante. Sin cambios. Test lo confirma.

### Movimientos sin referencia/stock_after — OK, sin arreglo parcial

El INSERT de `inventory.movements` en POS-Deli (líneas 398-403) sigue sin `reference_type`, `reference_id`, `stock_after`. Test lo confirma con assertions `Assert.Null`.

### POS sin idempotencia — OK, sin arreglo parcial

Sin `Idempotency-Key`, sin `pg_advisory_xact_lock`. Tests de retry confirman que se crean 2 ventas.

### Fix de UnitCost en movimientos — observación

El diff incluye un cambio no listado en el scope: `UnitCost = item.UnitPrice` → `UnitCost = item.CostPrice` (PosDeliController línea 424). Este fix **es correcto** — antes se registraba el *precio de venta* como *costo* en el movimiento de inventario, lo cual era un bug contable. Ahora usa `product.costprice`. Test `CreateSale_Movement_Uses_Real_Cost_...` verifica `Assert.Equal(10m, movement.UnitCost)` (el cost_price seeded es 10).

**Veredicto**: **OK** — fix correcto y cubierto, no complica Bloque 1.2.

---

## Resumen de hallazgos

| # | Hallazgo | Severidad |
|---|---|---|
| 1 | Policies son puras, sin dependencias de infraestructura | **OK** |
| 2 | PaymentPolicy: 4 categorías contables correctas, nequi legacy → transfer | **OK** |
| 3 | Rechazo de métodos desconocidos | **OK** |
| 4 | Nequi semánticamente idéntico en todos los flujos | **OK** |
| 5 | Rounding no altera órdenes históricas | **OK** |
| 6 | Rounding AwayFromZero vs ToEven divergente entre Checkout y Refund | **MEDIUM** |
| 7 | Tolerancia 0.01 consistente (POS-Deli se endureció de $1 a $0.01, intencional) | **OK** |
| 8 | SaleItemPolicy snapshots server-side, frontend no controla precios | **OK** |
| 9 | Rechazo de >2 decimales coherente con NUMERIC(18,2) | **OK** |
| 10 | InventoryMovementPlanner puro, sin persistencia/locks | **OK** |
| 11 | CheckoutRepository: una conexión, una transacción, mismo orden de locks | **OK** |
| 12 | Credit cash_in y Refund cash_out semántica preservada | **OK** |
| 13 | `NormalizePaymentMethod` residual en replay — dead-logic parcial | **LOW** |
| 14 | `CalculateSubtotal` usa dummy snapshot — fragilidad menor | **LOW** |
| 15 | Bugs pendientes: ninguno arreglado parcialmente | **OK** |
| 16 | Tests de caracterización prueban comportamiento, no implementación | **OK** |
| 17 | Fix de UnitCost→CostPrice en movimientos POS (no listado pero correcto) | **OK** |

---

## BLOQUE 1.1 REVISIÓN APROBADA

No se encontraron BLOCKERs ni HIGHs. Los 2 hallazgos MEDIUM y LOW son deuda aceptable documentada para bloques futuros:
- **MEDIUM**: Unificar rounding `AwayFromZero` en RefundRepository cuando se toque ese módulo.
- **LOW**: Eliminar `NormalizePaymentMethod` residual; refactorizar `CalculateSubtotal` dummy.

El bloque es seguro para commit. Los bugs deliberadamente pendientes están intactos y documentados con tests de caracterización que facilitarán su corrección en Bloques 1.2 y 1.3.
