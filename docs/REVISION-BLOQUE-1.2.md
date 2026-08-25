# Revision Independiente — Bloque 1.2 de Fase 1

> **Linea base**: `f5f78cc - refactor: add canonical sales policies`
> **Fecha**: Agosto 2026
> **Metodo**: Lectura exhaustiva de diff + archivos nuevos + tests. Sin ejecucion.
> **Suite reportada**: 474 PASS / 0 FAIL / 0 SKIP

---

## 1. Atomicidad

### InventoryTransactionWriter — OK

`InventoryTransactionWriter.ApplyAsync` recibe `IDbConnection connection, IDbTransaction transaction` como parametros (lineas 16-18). No crea conexion propia. No hace commit ni rollback. Cada operacion (lock, update stock, insert movement) usa el `transaction` recibido.

### SaleInventoryPlanBuilder — OK

`SaleInventoryPlanBuilder.BuildAsync` recibe `IDbConnection connection, IDbTransaction transaction` (lineas 28-32). La query de recipes (linea 72-90) usa el `transaction` recibido. No abre conexion propia. No hace commit ni rollback.

### Restaurante (CheckoutRepository.ProcessAsync) — OK

Linea 33-34: una conexion, una transaccion.
Linea 68: `BuildInventoryMovementsAsync(connection, transaction, ...)` — pasa la transaccion.
Linea 69-77: `_inventoryWriter.ApplyAsync(connection, transaction, ...)` — pasa la transaccion.
Commit en linea 88. Rollback en catch linea 96-100.

La cadena completa es: `ProcessAsync` abre conexion/transaccion -> `BuildInventoryMovementsAsync` -> `_inventoryPlanBuilder.BuildAsync(conn, tx)` -> vuelve -> `_inventoryWriter.ApplyAsync(conn, tx)` -> todo bajo la misma tx.

### POS-Deli (PosDeliController.CreateSale) — OK

Linea 186 (approx): `connection.BeginTransaction()`.
Linea 398-416: `_inventoryPlanBuilder.BuildAsync(connection, transaction, ...)`.
Linea 418-425: `_inventoryWriter.ApplyAsync(connection, transaction, ...)`.
Commit en linea 427. Rollback en catch lineas 442-451.

Misma cadena: una conexion, una transaccion, sin helpers que abran conexiones escondidas.

### Caminos de error despues de cada paso — OK

| Punto de fallo | Que ya se escribio | Revierte? |
|---|---|---|
| Despues del primer decremento stock | stock row decrementada | Si — rollback revierte UPDATE |
| Despues del segundo decremento stock | dos stock rows decrementadas | Si — rollback revierte ambos UPDATEs |
| Despues de movement INSERT | stock + movements | Si — rollback revierte todo |
| Despues de pagos INSERT | stock + movements + payments | Si — rollback revierte todo |
| Despues de caja UPDATE | stock + movements + payments + caja | Si — rollback revierte todo |

Test `CreateSale_Movement_Insert_Failure_Rolls_Back_Stock_And_Sale` (linea 595-627) verifica esto con un trigger que fuerza fallo en `inventory.movements`. Assert: stock vuelve a 2m, 0 ordenes.

Test `CreateSale_Insufficient_Stock_Rolls_Back_All_Sale_Writes` (linea 630-645) verifica rollback cuando stock es insuficiente despues de que ya se insertaron tabla, orden, items, pagos y caja.

**Veredicto**: **OK** — atomicidad completa, una sola transaccion end-to-end.

---

## 2. Locks y deadlocks

### Agregacion previa de requirements — OK

`InventoryTransactionWriter.LockAndValidateAvailabilityAsync` (linea 96-136):
- Linea 102: `productIds = requirements.Keys.OrderBy(id => id).ToArray()` — **orden determinístico por product_id**.
- Linea 103-112: `SELECT ... FROM inventory.stock WHERE product_id = ANY(@ProductIds) ORDER BY product_id FOR UPDATE` — lock en orden ascendente.
- Linea 31-34: `requirements` se construye agrupando por `ProductId` y sumando cantidades. Un producto que aparece 3 veces en la venta genera **un solo lock** con la cantidad total.

### Mismo orden en Restaurante y POS — OK

Ambos flujos delegan a `InventoryTransactionWriter.ApplyAsync`, que internamente llama a `LockAndValidateAvailabilityAsync`. El orden de locks es identico: `ORDER BY product_id`.

Ademas, dentro de `ApplyAsync` linea 27-30, los plans se procesan en orden: `.OrderBy(plan => plan.ProductId).ThenBy(plan => plan.MovementType)`. Esto garantiza que los decrementos individuales (linea 44-62) tambien se hacen en orden determinístico.

### Interaccion con caja — OK

**Restaurante**: Lock de caja en linea 64 (`LockActiveRegisterAsync` con `FOR UPDATE`). Esto ocurre **antes** del lock de stock (linea 69-77 `ApplyAsync`).
Orden: table -> order -> items -> **caja** -> stock.

**POS-Deli**: Lock de caja en linea 355-396 (`FOR UPDATE` en `cash_registers`). Esto ocurre **antes** del inventario (linea 398-425).
Orden: productos (sin lock) -> pagos/caja -> stock.

Ambos flujos: caja primero, stock despues. **Sin inversion**.

### Interaccion con Refund — OK

`RefundRepository.ProcessAsync`:
1. `pg_advisory_xact_lock` por idempotency key (linea 194-197)
2. `FOR UPDATE` en `sales.orders` (linea 211)
3. `RestoreInventoryAsync` — `INSERT ... ON CONFLICT DO UPDATE` en `inventory.stock` (linea 724-734). **No usa `FOR UPDATE`**, usa upsert atomico.

Refund **suma** stock (inbound). Sale **resta** stock (outbound con `FOR UPDATE`).
El refund nunca toma `FOR UPDATE` en `inventory.stock`, asi que no puede hacer deadlock con una venta concurrente. El upsert atomico de PostgreSQL garantiza serializacion a nivel de fila.

### Interaccion con Purchase Receipt — OK

`PurchaseOrderRepository` (linea 276-284): Tambien usa `INSERT ... ON CONFLICT DO UPDATE` para sumar stock. Mismo patron que Refund. No usa `FOR UPDATE`. Sin riesgo de deadlock con ventas.

### Test de deadlock — OK

`CreateSale_Deterministic_Product_Locks_Avoid_Deadlock` (linea 564-592): Dos ventas concurrentes con productos A,B y B,A respectivamente. Gate en payment para forzar concurrencia real. Assert: ambas completan sin deadlock, stock = 0 para ambos.

**Veredicto**: **OK** — locks determinísticos por product_id, sin inversiones detectadas entre modulos.

---

## 3. Committed Inventory

### CommittedInventorySql — analisis exhaustivo

El CTE tiene dos partes unidas con `UNION ALL`:

**Parte 1 — productos simples** (lineas 7-22):
```sql
SELECT oi.product_id, oi.quantity
FROM sales.orders o
JOIN sales.tables t ON t.id = o.table_id AND t.company_id = o.company_id
JOIN sales.order_items oi ON oi.order_id = o.id AND oi.company_id = o.company_id
JOIN inventory.products product ON product.id = oi.product_id AND product.company_id = oi.company_id
WHERE o.company_id = @CompanyId AND o.branch_id = @BranchId
  AND o.id <> @ExcludedOrderId
  AND o.status = 'pending' AND t.status = 'open' AND t.deleted_at IS NULL
  AND COALESCE(product.product_type, 'simple') <> 'prepared'
  AND product.track_stock = TRUE
```

**Parte 2 — prepared -> ingredientes** (lineas 26-46):
```sql
SELECT recipe.ingredient_id, oi.quantity * recipe.quantity
FROM sales.orders o
JOIN sales.tables t ...
JOIN sales.order_items oi ...
JOIN inventory.products prepared ON prepared.id = oi.product_id ...
JOIN inventory.recipes recipe ON recipe.product_id = prepared.id ...
JOIN inventory.products ingredient ON ingredient.id = recipe.ingredient_id ...
WHERE ... AND prepared.product_type = 'prepared'
  AND ingredient.track_stock = TRUE
```

**Agregacion** (lineas 48-54):
```sql
committed AS (
    SELECT @BranchId::bigint AS branch_id, product_id,
           SUM(quantity) AS committed_quantity
    FROM committed_lines GROUP BY product_id
)
```

### Verificaciones punto por punto:

| Criterio | Resultado |
|---|---|
| `available = physical - committed` | **OK** — `LockAndValidateAvailabilityAsync` linea 134: `stock.Quantity - committedQuantity < requirement.Value` |
| Ordenes que entran en committed | **OK** — solo `o.status = 'pending'` |
| Estados de mesa que entran | **OK** — solo `t.status = 'open'` y `t.deleted_at IS NULL` |
| Checkout excluye su propia orden | **OK** — `ExcludedCommittedOrderId: order.Id` en linea 76 de CheckoutRepository. Se pasa como `@ExcludedOrderId` en la query (linea 17, 41: `o.id <> @ExcludedOrderId`) |
| POS no excluye ordenes | **OK** — POS pasa `ExcludedCommittedOrderId` sin valor (default `null`), que en linea 124 se convierte a `-1L`. Ningun order tiene id=-1, asi que no excluye nada |
| Prepared expande a ingredientes | **OK** — Parte 2 del UNION ALL hace `oi.quantity * recipe.quantity` |
| Cantidades repetidas se agrupan | **OK** — `GROUP BY product_id` con `SUM(quantity)` en la CTE `committed` |
| Tenant/branch scope | **OK** — `o.company_id = @CompanyId AND o.branch_id = @BranchId` en ambas partes. JOINs con `company_id` en todas las tablas |
| Doble conteo | **OK** — No hay doble conteo. `COALESCE(product.product_type, 'simple') <> 'prepared'` en Parte 1 excluye prepared. `prepared.product_type = 'prepared'` en Parte 2 solo incluye prepared. Son mutuamente excluyentes |

### Test de committed inventory

- `CreateSale_Respects_Stock_Committed_By_Open_Table` (linea 649): stock=1, orden pendiente con qty=1, POS intenta vender 1 -> `BusinessException`. Stock queda en 1.
- `Checkout_Excludes_Its_Own_Commitment_From_Availability` (CheckoutRepositoryIntegrationTests): stock=1, su propia orden pendiente con qty=1 -> checkout succeeds. Stock queda en 0.
- `Checkout_Respects_Other_Open_Order_Commitment` (CheckoutRepositoryIntegrationTests): stock=1, otra orden pendiente con qty=1 -> `BusinessException`. Stock queda en 1.
- `GetStockByBranchAsync_Expands_Open_Prepared_Order_Into_Ingredient_Commitment` (InventoryRepositoryIntegrationTests): prepared con receta de 2 unidades ingrediente, stock=10 -> `ReservedQuantity=2`, `AvailableQuantity=8`.

**Veredicto**: **OK** — committed inventory correcto, sin doble conteo, expansion de prepared verificada.

---

## 4. Carrera orden abierta vs POS

### Escenario exacto para reproducirla

1. Mesa Restaurante abierta con orden pendiente que tiene Producto X (qty=1). Stock fisico = 2.
2. Committed = 1 (por la orden pendiente).
3. **Mesero agrega mas items** a la orden (via `AddItemsAsync` o `UpdateItemQuantityAsync`): agrega Producto X qty=1 mas. Ahora la orden tiene qty=2.
4. **Concurrentemente**, POS-Deli intenta vender Producto X qty=1.

**Problema**: `AddItemsAsync` (linea 103-184) y `UpdateItemQuantityAsync` (linea 187-239) **no toman lock en `inventory.stock`**. Solo lockan `sales.tables` y `sales.orders`. No verifican disponibilidad de stock.

POS-Deli, al ejecutar `LockAndValidateAvailabilityAsync`, ve:
- stock fisico = 2
- committed = 1 (snapshot del committed ANTES de que el mesero termine su UPDATE)
- available = 2 - 1 = 1 >= 1 requerido -> **aprueba la venta**

Microsegundos despues, el mesero commit su cambio. Ahora:
- stock fisico = 1 (POS lo decrementó)
- committed real = 2 (orden ahora tiene qty=2)
- available real = 1 - 2 = **-1** (oversold conceptualmente)

### Peor resultado posible

El Checkout posterior de la mesa Restaurante encontrara stock insuficiente y **fallara con `BusinessException`**. El mesero ya tomo el pedido, el comensal espera su comida, pero el sistema no permite facturar.

**No produce stock fisico negativo** en la tabla `inventory.stock`, porque `InventoryTransactionWriter` tiene `quantity >= @Quantity` en el `UPDATE ... RETURNING` (linea 51). El checkout simplemente fallara.

### Puede producir overselling conceptual?

**Si**. El stock fisico se vendio via POS pero estaba conceptualmente reservado para la mesa. Es un overselling logico, no fisico.

### Puede producir stock fisico negativo?

**No**. El `WHERE quantity >= @Quantity` en el decremento lo impide. El checkout de la mesa fallara en vez de decrementar a negativo.

### El checkout posterior detectaria el problema?

**Si**. `InventoryTransactionWriter.ApplyAsync` lanzara `BusinessException("Stock insuficiente para el producto X")`. La mesa queda sin facturar.

### Ventana de la carrera

La ventana es estrecha pero real: solo ocurre si `AddItemsAsync`/`UpdateItemQuantityAsync` y una venta POS del mismo producto se ejecutan concurrentemente. La ventana existe porque `AddItemsAsync` no toma `FOR UPDATE` en `inventory.stock`.

### Es nuevo este riesgo?

**Parcialmente**. En Bloque 1.1, POS no consultaba committed en absoluto — vendia directo sin considerar reservas. Ahora POS SI consulta committed, lo cual **reduce** la ventana. Pero la carrera con mutaciones de orden sigue existiendo porque esas mutaciones no toman lock de stock.

### Severidad

**MEDIUM** — La ventana es estrecha (requiere concurrencia exacta entre modificacion de orden y venta POS del mismo producto). El sistema falla de forma segura (checkout rechaza, no corrompe datos). La correccion correcta es que `AddItemsAsync`/`UpdateItemQuantityAsync` validen disponibilidad de stock, lo cual corresponde a un bloque futuro.

**No debe bloquear aprobacion de 1.2** porque:
1. El comportamiento es estrictamente mejor que la linea base (donde POS ignoraba committed completamente).
2. El fallo es seguro (no corrompe datos, no produce stock negativo).
3. La ventana requiere concurrencia precisa entre dos operaciones infrecuentes.
4. La correccion es independiente y puede hacerse en 1.3 o posterior sin romper nada de lo construido.

**Veredicto**: **MEDIUM** — deuda aceptable, documentada.

---

## 5. Prepared / Recipes

### Recipe tenant-safe — OK

`SaleInventoryPlanBuilder` linea 86: `WHERE recipe.company_id = @CompanyId`. Los JOINs a `inventory.products` tambien usan `company_id`:
- Linea 83: `prepared.company_id = recipe.company_id`
- Linea 85: `ingredient.company_id = recipe.company_id`

`CommittedInventorySql` Parte 2: mismos JOINs con `company_id` en recipes, prepared, ingredient.

### Ingredient tenant-safe — OK

Verificado arriba. Ademas, la query de productos en POS (linea 230): `WHERE p.company_id = @CompanyId`.

### Ingredientes activos / no eliminados — OK

`SaleInventoryPlanBuilder` linea 80: `ingredient.is_active AS IsActive`.
Linea 79-80: `ingredient.deleted_at AS DeletedAt` (implicitamente por el query, ya que `deleted_at IS NULL` no esta en el WHERE de recipes).
Linea 98: `if (recipes.Any(recipe => recipe.Quantity <= 0 || !recipe.IsActive || recipe.DeletedAt.HasValue))` — **rechaza ingredientes inactivos o eliminados**.

Test `CreateSale_Prepared_With_Inactive_Ingredient_Is_Rejected_Without_Writes` (linea 379-397) verifica esto.

### track_stock — OK

Linea 77: `ingredient.track_stock AS TrackStock`. Se pasa a `InventoryMovementPlanner.Create` con `requiresStock: group.Key.TrackStock` (linea 121). Solo ingredientes con `track_stock=TRUE` generan lock y decremento.

### Cantidades — OK

Linea 117: `quantity: group.Sum(recipe => recipe.Quantity * soldByProduct[recipe.ProductId])`. Multiplica cantidad de receta por cantidad vendida del prepared.

### Ingredientes repetidos (dos prepared compartiendo ingrediente) — OK

Linea 107-112: Se agrupa por `IngredientId, TrackStock, UnitCost`. Si dos prepared usan el mismo ingrediente con mismo costo, se produce **un solo plan** con la suma de cantidades. Un solo lock, un solo decremento.

Test `CreateSale_Prepared_Products_Group_Repeated_Ingredient` (linea 401-433): Dos preparados comparten ingrediente (2 + 3 = 5 unidades). Assert: stock baja de 10 a 5, **un solo movement** con quantity=5 y stock_after=5.

### Prepared sin receta — OK

Linea 92-96: `if (!recipes.Any(recipe => recipe.ProductId == preparedId)) throw new BusinessException(...)`.

Test `CreateSale_Prepared_Without_Recipe_Is_Rejected_Without_Writes` (linea 364-376): Assert `BusinessException`, 0 ordenes.

Test `Prepared_Product_Without_Recipe_Is_Rejected_And_Rolled_Back` en CheckoutRepositoryIntegrationTests: Elimina receta despues del seed, verifica `BusinessException`, mesa queda open, 0 movements.

### Costo real del ingrediente — OK

Linea 78: `ingredient.cost_price AS UnitCost`. Se usa como `unitCost` en `InventoryMovementPlanner.Create` (linea 118). No usa el precio de venta del prepared.

Test en CheckoutRepositoryIntegrationTests linea 67: `Assert.Equal(20m, ...)` para unit_cost del movement (donde cost_price del ingrediente es 20).

### Movement type recipe_consumption — OK

Linea 116: `movementType: "recipe_consumption"`. Verificado en test linea 65: `Assert.Equal("recipe_consumption", ...)`.

### Simetria con Refund — OK

`RefundRepository.RestoreInventoryAsync` (linea 670-711):
- Para prepared: consulta recipes, multiplica `QuantityPerProduct * refundItem.Quantity`, restaura cada ingrediente con `track_stock`.
- Movement type: `"refund_recipe"` (linea 701).
- Usa `INSERT ... ON CONFLICT DO UPDATE` para sumar stock.

La simetria es correcta:
| Venta | Refund |
|---|---|
| `recipe_consumption` | `refund_recipe` |
| Decrementa `quantity - X` | Incrementa `quantity + X` |
| `SaleInventoryPlanBuilder` expande recetas | `RestoreInventoryAsync` expande recetas |
| Usa `ingredient.cost_price` | Usa `0` como unit_cost para recipe refund |

**Nota**: Refund usa `unit_cost = 0` para `refund_recipe` (linea 701: tercer parametro es `0`). Esto difiere de la venta que usa `ingredient.cost_price`. Es una asimetria menor — el costo del movimiento de refund de receta no tiene significado contable directo. **LOW**.

**Veredicto**: **OK** — recipes correctas, tenant-safe, simetricas con Refund.

---

## 6. Movements

### Verificacion para producto simple

`InventoryTransactionWriter.ApplyAsync` linea 69-92:

| Campo | Valor | Correcto? |
|---|---|---|
| `movement_type` | Del plan: `"sale"` para simples | OK |
| `quantity` (signo) | Siempre positivo. El tipo `"sale"` implica salida | OK |
| `unit_cost` | Del plan: `product.cost_price` via `SaleInventoryLine.UnitCost` | OK |
| `reference_type` | Del plan: `"order"` (SaleInventoryPlanContext linea 473/403) | OK |
| `reference_id` | Del plan: `order.Id` / `orderId` | OK |
| `stock_after` | `RETURNING quantity` del UPDATE (linea 52), pasado via `WithStockAfter` (linea 66) | OK |
| `company_id` | `context.CompanyId` | OK |
| `branch_id` | `context.BranchId` | OK |
| `created_by` | `context.UserId` | OK |

### Verificacion para prepared (recipe_consumption)

Misma tabla, mismos campos. `movement_type = "recipe_consumption"`, `unit_cost = ingredient.cost_price`, `quantity = recipe.quantity * sold_quantity`.

### stock_after corresponde al RETURNING — OK

Linea 44-52: `UPDATE inventory.stock SET quantity = quantity - @Quantity ... RETURNING quantity`.
Linea 65-67: `var completedPlan = stockAfter.HasValue ? InventoryMovementPlanner.WithStockAfter(plan, stockAfter.Value) : plan`.
Linea 90: `completedPlan.StockAfter` se inserta en el movement.

El `stock_after` es **exactamente** el valor retornado por el `RETURNING` de la misma fila actualizada, dentro de la misma transaccion. No hay posibilidad de lectura fantasma.

### Productos sin track_stock — OK

Linea 42: `if (plan.RequiresStock)` — solo decrementa y captura `stock_after` si `RequiresStock`. Para productos sin tracking, `stockAfter` queda `null`, y el movement se inserta con `StockAfter = null`.

Test `CreateSale_WeightedDecimal_PersistsEconomics_StockPayment_AndCashRegister`: verifica movement completo para producto con track_stock.

Test `CreateSale_Movement_Contains_Real_Cost_Reference_And_StockAfter` (linea 436-458): Assert `UnitCost=10m` (cost_price), `ReferenceType="order"`, `ReferenceId=saleId`, `StockAfter=4m`.

**Veredicto**: **OK** — movements completos y correctos.

---

## 7. POS-Deli

### Rechaza producto inactivo — OK

`SaleInventoryPlanBuilder` linea 43: `if (!line.IsActive || !line.IsForSale) throw ValidationException`.
POS ahora pasa `IsActive` y `IsForSale` desde la query de productos (linea 227-228).
Test `CreateSale_Inactive_Product_Is_Rejected_Without_Writes` (linea 194).

### Rechaza producto eliminado — OK

La query de productos (linea 232): `AND p.deleted_at IS NULL`. Producto eliminado no aparece en resultados -> `ValidationException("Producto no encontrado")` en linea 243-244.

### Rechaza is_for_sale=false — OK

Misma validacion en `SaleInventoryPlanBuilder` linea 43.
Test `CreateSale_NotForSale_Product_Is_Rejected_Without_Writes` (linea 209).

### Rechaza otro tenant — OK

Query linea 230: `WHERE p.company_id = @CompanyId`. Producto de otro tenant no aparece.
Test `CreateSale_Product_From_Another_Tenant_Is_Rejected_Without_Writes` (linea 224).

### Rechaza stock insuficiente — OK

`InventoryTransactionWriter` linea 134: `stock.Quantity - committedQuantity < requirement.Value -> BusinessException`.
Ademas linea 51: `WHERE quantity >= @Quantity` en el UPDATE con RETURNING.
Test `CreateSale_Insufficient_Stock_Rolls_Back_All_Sale_Writes` (linea 630).

### Rechaza prepared sin receta — OK

`SaleInventoryPlanBuilder` linea 94-95.
Test `CreateSale_Prepared_Without_Recipe_Is_Rejected_Without_Writes` (linea 364).

### NO se arreglo idempotencia/replay — OK

Test `CreateSale_CurrentBehavior_Retry_Creates_A_Second_Sale_Pending_Block_13` (linea 461-475): Mismo request dos veces -> 2 ordenes, stock decrementado dos veces. Confirma que la idempotencia sigue pendiente para 1.3.

Test `CreateSale_CurrentBehavior_Different_Retry_Payload_Creates_Another_Sale_Pending_Block_13` (linea 478-491): Payloads diferentes -> 2 ordenes. Confirma que no hay proteccion anti-replay.

**Veredicto**: **OK** — POS-Deli ahora valida todo lo requerido, sin arreglo accidental de idempotencia.

---

## 8. Restaurante — equivalencia

### Checkout — OK

La unica diferencia funcional es que el inventario ahora se delega a `SaleInventoryPlanBuilder` + `InventoryTransactionWriter` en vez de logica inline. Verifico equivalencia:

| Aspecto | Antes (inline) | Ahora (delegado) |
|---|---|---|
| Agrupacion por ProductId | `GroupBy(item => new { item.ProductId, ... })` | Identico en `SaleInventoryPlanBuilder` linea 49 |
| Recipe expansion | Query inline con JOINs a recipes | Identico query en `SaleInventoryPlanBuilder` linea 72-90 |
| Lock order | `ORDER BY product_id FOR UPDATE` | Identico en `InventoryTransactionWriter` linea 109 |
| Decremento con guard | `WHERE quantity >= @Quantity RETURNING quantity` | Identico en linea 51 |
| Movement INSERT | Campos iguales | Identico en linea 69-92, ahora con `reference_type` y `stock_after` |
| Committed stock exclusion | **No existia** | **Nuevo**: excluye su propia orden |

### Committed (nuevo) — OK

Antes: El checkout solo verificaba `stock.Quantity < requirement.Value`. No consideraba committed.
Ahora: Verifica `stock.Quantity - committedQuantity < requirement.Value` (linea 134), excluyendo su propia orden (linea 76: `ExcludedCommittedOrderId: order.Id`).

Esto es **estrictamente mejor** — antes, si dos mesas tenian el mismo producto y stock era 1, ambos checkouts podian intentar facturar. Ahora el segundo ve committed del primero (si el primero aun esta pending).

### Recetas — cambio

Antes: La query de recetas filtraba `ingredient.deleted_at IS NULL` pero **no verificaba** `ingredient.is_active`.
Ahora: `SaleInventoryPlanBuilder` linea 98 verifica `!recipe.IsActive || recipe.DeletedAt.HasValue`.

Esto es un **endurecimiento correcto** — un ingrediente inactivo ahora bloquea la venta del prepared.

### Costo — cambio

Antes: `LoadItemsAsync` no cargaba `cost_price`. El movimiento usaba el `UnitPrice` (precio de venta) del item.
Ahora: `LoadItemsAsync` linea 311 carga `p.cost_price AS CostPrice`. La `SaleInventoryLine` usa `item.CostPrice`.

Test en CheckoutRepositoryIntegrationTests linea 68: `Assert.Equal(20m, ...)` verifica que `unit_cost` en movement es el cost_price (20m), no el sale_price.

Esto es la **correccion del bug de UnitCost que se detecto en el review de 1.1** — ahora completamente integrada.

### Movement logging — cambio

Antes: Los movements no tenian `reference_type` ni `stock_after` (solo se insertaba `reference_id`).
Ahora: `reference_type = "order"`, `stock_after` del RETURNING.

Test en CheckoutRepositoryIntegrationTests linea 69-72: Assert `stock_after = 9m`.

### Replay — sin cambios

`TryBuildReplayAsync` sigue igual. El replay no toca inventario (la venta ya fue procesada). Sin regresion.

### Pagos — sin cambios

`InsertPaymentsAsync`, `Calculate`, `UpdateCashRegisterAsync` — sin cambios en este bloque.

### Credito — sin cambios

`InsertCreditAsync` — sin cambios.

### Orden/mesa — sin cambios

`CompleteOrderAsync`, `CompleteTableAsync` — sin cambios.

**Veredicto**: **OK** — equivalencia mantenida, cambios son estrictamente mejoras (committed, costo real, movement logging, ingredientes inactivos).

---

## 9. Precision — propuesta futura

### `sales.order_items.quantity` NUMERIC(18,2) -> NUMERIC(18,3)

`SaleItemPolicy`:
```csharp
public const int CurrentQuantityDecimals = 2;
public const int TargetQuantityDecimals = 3;
```

`InventoryMovementPlanner`:
```csharp
public const int InventoryQuantityDecimals = 3;
```

La policy de venta rechaza >2 decimales hoy. Pero el planner de inventario ya normaliza a 3 decimales (linea 65 del planner: `NormalizeQuantity` con precision 3). Esto es correcto porque las cantidades de receta (`recipe.quantity * sold_quantity`) pueden producir 3 decimales incluso con cantidades de venta de 2 decimales.

### Migracion necesaria

Para habilitar 3 decimales en ventas:
1. `ALTER TABLE sales.order_items ALTER COLUMN quantity TYPE NUMERIC(18,3)` — la columna `subtotal` es `GENERATED ALWAYS AS (quantity * unit_price)`, que deberia recalcularse automaticamente.
2. `ALTER TABLE sales.refund_items ALTER COLUMN quantity TYPE NUMERIC(18,3)` — si existe restriccion similar.
3. Verificar indices que incluyan `quantity`.
4. Cambiar `SaleItemPolicy.CurrentQuantityDecimals` de 2 a 3.

### Riesgos

- `subtotal` como `GENERATED ALWAYS AS (quantity * unit_price)`: PostgreSQL recalcula automaticamente al alterar la columna. Pero si `unit_price` es NUMERIC(18,2), el producto `NUMERIC(18,3) * NUMERIC(18,2)` produce NUMERIC — sin perdida.
- Delivery: No tiene interaccion con `order_items.quantity` directamente. No afectado.
- Refund: `refund_items.quantity` necesitaria la misma migracion.

### Estado actual

No hay migracion implementada en este bloque. `SaleItemPolicy` rechaza >2 decimales. El campo `InventoryQuantityDecimals = 3` es solo para normalizacion interna de recetas.

**Veredicto**: **OK** — propuesta futura correctamente preparada, sin impacto actual.

---

## 10. Tests de concurrencia

### CreateSale_Concurrent_Sales_Do_Not_Persist_Negative_Stock (linea 494)

**Concurrencia real**: Si. Usa `RunBehindPaymentGateAsync` que crea un `pg_advisory_xact_lock` en un trigger `BEFORE INSERT ON sales.order_payments`. Ambas ventas se bloquean en el trigger, se verifica que estan bloqueadas via `pg_stat_activity WHERE wait_event = 'advisory'`, y luego se libera el gate.

**Depende de timing?** Minimamente. El test espera hasta 10 segundos para que ambas transacciones lleguen al gate (linea ~925-935). Verifica con `blocked >= actions.Length` antes de liberar. **Robusto**.

**Advisory locks alteran comportamiento productivo?** No. El trigger se crea y elimina dentro del test (lineas 905-955). No existe en produccion. El gate fuerza que ambas transacciones esten "en vuelo" simultaneamente antes de liberar, lo cual es la forma correcta de testar concurrencia sin depender de timing.

### CreateSale_Two_Concurrent_Pos_Sales_Consume_Exact_Stock (linea 513)

Stock=2, dos ventas de qty=1 cada una. Assert: ambas completan (null errors), stock=0, 2 movements. Verifica el caso feliz de concurrencia sin conflicto.

### CreateSale_Two_Prepared_Products_Serialize_Shared_Ingredient (linea 535)

Stock ingrediente=3, prepared A necesita 2, prepared B necesita 2. Total=4 > 3. Assert: una completa, otra falla. Stock=1, 1 movement.

### CreateSale_Deterministic_Product_Locks_Avoid_Deadlock (linea 564)

Request 1: [A, B]. Request 2: [B, A]. Ambos deben adquirir locks en el mismo orden (por product_id ASC). Assert: ambos completan sin deadlock.

### CreateSale_Movement_Insert_Failure_Rolls_Back_Stock_And_Sale (linea 595)

Trigger que fuerza fallo en `inventory.movements`. Verifica rollback atomico.

### Restaurant_And_Pos_Competing_For_Reserved_Stock_Preserve_Restaurant_Order (linea 672)

Stock=1, orden pendiente reserva 1. Checkout Restaurante y POS corren concurrentemente via `Task.WhenAll`. Assert: Restaurante gana (su committed se excluye), POS falla, stock=0, 1 orden completed.

**Nota**: Este test no usa `RunBehindPaymentGateAsync`, usa `Task.WhenAll` directo. Es menos determinístico que los tests con gate — el resultado depende de cual transaccion adquiere los locks primero. Pero el assert es que **exactamente uno** gana, lo cual es correcto independientemente de quien gane primero.

### Escenario faltante

No hay test para la carrera documentada en seccion 4: `AddItemsAsync` concurrente con una venta POS del mismo producto. Este es el unico escenario de concurrencia critico no cubierto.

**Veredicto**: **OK** — tests de concurrencia son robustos, usan mecanismos reales de PostgreSQL, no dependen excesivamente de timing. El gate pattern es la forma correcta de forzar concurrencia determinística.

---

## Resumen de hallazgos

| # | Hallazgo | Severidad |
|---|---|---|
| 1 | Atomicidad: una conexion, una transaccion, sin helpers con conexion propia | **OK** |
| 2 | InventoryTransactionWriter nunca abre conexion, nunca hace commit/rollback | **OK** |
| 3 | SaleInventoryPlanBuilder nunca abre conexion, nunca hace commit/rollback | **OK** |
| 4 | Rollback en todos los caminos de error (stock, movement, pago, caja) | **OK** |
| 5 | Locks determinísticos por product_id ASC con agregacion previa | **OK** |
| 6 | Sin inversiones de lock entre Sale, Refund y Purchase Receipt | **OK** |
| 7 | CommittedInventorySql: tenant-safe, branch-safe, sin doble conteo, expansion prepared | **OK** |
| 8 | Checkout excluye su propia orden del committed | **OK** |
| 9 | POS no excluye ordenes del committed (correcto) | **OK** |
| 10 | Carrera AddItemsAsync vs POS: ventana estrecha, fallo seguro, no corrompe datos | **MEDIUM** |
| 11 | Recipes: tenant-safe, ingredientes activos/no eliminados, cantidades agrupadas | **OK** |
| 12 | Prepared sin receta rechazado en ambos flujos | **OK** |
| 13 | Dos prepared compartiendo ingrediente: agrupacion correcta, un solo movement | **OK** |
| 14 | Refund simetrico: recipe expansion, movement type `refund_recipe` | **OK** |
| 15 | Refund usa unit_cost=0 para recipe refund vs venta usa ingredient.cost_price | **LOW** |
| 16 | Movements completos: type, quantity, unit_cost, reference_type, reference_id, stock_after | **OK** |
| 17 | stock_after = RETURNING de la misma fila, misma transaccion | **OK** |
| 18 | POS rechaza: inactivo, eliminado, not-for-sale, otro tenant, stock insuficiente, sin receta | **OK** |
| 19 | POS NO arreglo idempotencia (correcto, es 1.3) | **OK** |
| 20 | Restaurante: equivalencia funcional mantenida, mejoras estrictas | **OK** |
| 21 | Restaurante: committed stock es nuevo comportamiento, estrictamente mejor | **OK** |
| 22 | Restaurante: ingrediente inactivo ahora bloquea checkout (endurecimiento correcto) | **OK** |
| 23 | Precision: propuesta futura preparada sin impacto actual | **OK** |
| 24 | Tests concurrentes: gate pattern robusto, concurrencia real, sin dependencia de timing | **OK** |
| 25 | Falta test para carrera AddItemsAsync vs venta POS | **LOW** |

---

## BLOQUE 1.2 REVISION APROBADA

No se encontraron BLOCKERs ni HIGHs. Los hallazgos MEDIUM y LOW son deuda aceptable:

- **MEDIUM**: Carrera entre `AddItemsAsync`/`UpdateItemQuantityAsync` y ventas POS concurrentes. La ventana es estrecha, el fallo es seguro (checkout rechaza, datos intactos), y la correccion (que esas mutaciones validen stock disponible) puede hacerse en un bloque posterior sin romper lo construido.
- **LOW**: Refund usa `unit_cost=0` para `refund_recipe` mientras la venta usa `ingredient.cost_price`. Asimetria contable menor que no afecta stock ni montos.
- **LOW**: Falta test de concurrencia para la carrera `AddItemsAsync` vs POS.

El bloque es seguro para commit. La migracion de inventario al nucleo comun esta bien ejecutada, ambos flujos comparten la misma infraestructura transaccional, y las 474 pruebas cubren los escenarios criticos.
