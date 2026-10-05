# AUDITORÍA V1 — REFUNDS DE PREPARADOS E INVENTARIO HISTÓRICO

> **Fecha**: 2025-01-15  
> **Auditor**: Arquitectónico independiente  
> **Alcance**: Análisis de evidencia sin modificaciones  
> **Objetivo**: Determinar estrategia correcta para refunds cuando la receta cambia post-venta

---

## RESUMEN EJECUTIVO

**BUG CONFIRMADO**: El sistema actual **relee la receta vigente** al momento del refund, no la receta histórica que se usó en la venta original. Esto causa **incorrecta restitución de inventario** cuando la receta cambia entre la venta y el refund.

**EVIDENCIA ARQUITECTÓNICA**: El ledger de movimientos de inventario **SÍ registra** toda la información necesaria para implementar reversión de movimientos originales, pero el código de refund **NO la consulta**.

**RECOMENDACIÓN**: `REVERSIÓN DE MOVIMIENTOS ORIGINALES`

---

## 1. FLUJO DE VENTA ACTUAL — EVIDENCIA

### 1.1 Punto de entrada: CheckoutRepository.ProcessAsync

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/CheckoutRepository.cs`  
**Líneas**: 31-101

```csharp
public async Task<CheckoutResult> ProcessAsync(CheckoutCommand command)
{
    // ... validaciones ...
    
    var movements = await BuildInventoryMovementsAsync(connection, transaction, command, order, table, items);
    await _inventoryWriter.ApplyAsync(
        connection,
        transaction,
        new InventoryTransactionContext(
            command.CompanyId,
            command.BranchId,
            command.UserId,
            ExcludedCommittedOrderId: order.Id),
        movements);
    
    // ... resto del checkout ...
}
```

**Líneas críticas**: 68-77

### 1.2 Expansión de recetas: SaleInventoryPlanBuilder.BuildAsync

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Inventory/SaleInventoryPlanBuilder.cs`  
**Líneas**: 28-124

#### Flujo para productos preparados:

1. **Líneas 62-67**: Identifica productos con `product_type = 'prepared'`
2. **Líneas 72-90**: **EXPANDE LA RECETA** consultando `inventory.recipes`:

```csharp
var recipes = (await connection.QueryAsync<RecipeRequirementRow>(@"
    SELECT recipe.product_id AS ProductId,
           recipe.ingredient_id AS IngredientId,
           recipe.quantity AS Quantity,
           ingredient.name AS IngredientName,
           ingredient.track_stock AS TrackStock,
           ingredient.cost_price AS UnitCost,
           ingredient.is_active AS IsActive,
           ingredient.deleted_at AS DeletedAt
    FROM inventory.recipes recipe
    JOIN inventory.products prepared
      ON prepared.id = recipe.product_id AND prepared.company_id = recipe.company_id
    JOIN inventory.products ingredient
      ON ingredient.id = recipe.ingredient_id AND ingredient.company_id = recipe.company_id
    WHERE recipe.company_id = @CompanyId
      AND recipe.product_id = ANY(@PreparedIds)
      AND prepared.product_type = 'prepared'
    ORDER BY recipe.product_id, recipe.ingredient_id",
    new { context.CompanyId, PreparedIds = preparedIds }, transaction)).ToList();
```

3. **Líneas 106-122**: Crea movimientos de tipo `"recipe_consumption"` para cada ingrediente:

```csharp
plans.AddRange(recipes
    .GroupBy(recipe => new
    {
        recipe.IngredientId,
        recipe.TrackStock,
        recipe.UnitCost
    })
    .Select(group => InventoryMovementPlanner.Create(
        InventoryMovementDirection.Outbound,
        group.Key.IngredientId,
        movementType: "recipe_consumption",
        quantity: group.Sum(recipe => recipe.Quantity * soldByProduct[recipe.ProductId]),
        unitCost: group.Key.UnitCost,
        referenceType: context.ReferenceType,
        referenceId: context.ReferenceId,
        requiresStock: group.Key.TrackStock,
        notes: context.RecipeNotes)));
```

**HALLAZGO #1**: La venta **SÍ expande la receta** y calcula cantidades exactas de ingredientes.

### 1.3 Persistencia de movimientos: InventoryTransactionWriter.ApplyAsync

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Inventory/InventoryTransactionWriter.cs`  
**Líneas**: 16-93

```csharp
await connection.ExecuteAsync(@"
    INSERT INTO inventory.movements (
        company_id, branch_id, product_id, movement_type,
        quantity, unit_cost, reference_type, reference_id,
        notes, stock_after, created_by, created_at
    ) VALUES (
        @CompanyId, @BranchId, @ProductId, @MovementType,
        @Quantity, @UnitCost, @ReferenceType, @ReferenceId,
        @Notes, @StockAfter, @UserId, NOW()
    )",
    new
    {
        context.CompanyId,
        context.BranchId,
        completedPlan.ProductId,
        completedPlan.MovementType,
        completedPlan.Quantity,
        completedPlan.UnitCost,
        completedPlan.ReferenceType,
        completedPlan.ReferenceId,
        completedPlan.Notes,
        completedPlan.StockAfter,
        context.UserId
    }, transaction);
```

**HALLAZGO #2**: Cada movimiento persiste:
- `product_id` (ingrediente)
- `movement_type` (`"recipe_consumption"`)
- `quantity` (cantidad exacta descontada)
- `unit_cost`
- `reference_type` (`"order"`)
- `reference_id` (ID de la orden)
- `notes`
- `stock_after`
- `created_by`
- `created_at`

### 1.4 Esquema de tabla: inventory.movements

**Archivo**: `supabase/migrations/003_inventory_tables.sql`  
**Líneas**: 178-221

```sql
CREATE TABLE IF NOT EXISTS inventory.movements (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    product_id          BIGINT NOT NULL REFERENCES inventory.products(id),

    -- Tipo
    movement_type       VARCHAR(50) NOT NULL,

    -- Cantidades
    quantity            DECIMAL(18, 3) NOT NULL,
    unit_cost           DECIMAL(18, 2),
    total_cost          DECIMAL(18, 2) GENERATED ALWAYS AS (ABS(quantity) * COALESCE(unit_cost, 0)) STORED,

    -- Referencia al documento origen
    reference_type      VARCHAR(50),
    reference_id        BIGINT,

    -- Info adicional
    notes               VARCHAR(1000),

    -- Transferencias entre sucursales
    from_branch_id      BIGINT REFERENCES core.branches(id),
    to_branch_id        BIGINT REFERENCES core.branches(id),

    -- Stock despues del movimiento
    stock_after         DECIMAL(18, 3),

    -- IA
    created_by_ai       BOOLEAN DEFAULT FALSE,
    ai_confidence       DECIMAL(5, 2),
    ai_metadata         JSONB,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          BIGINT
);

CREATE INDEX IF NOT EXISTS idx_inv_movements_ref ON inventory.movements (reference_type, reference_id);
```

**HALLAZGO #3**: La tabla tiene índice compuesto `(reference_type, reference_id)` que permite consultar eficientemente todos los movimientos de una orden.

---

## 2. FLUJO DE REFUND ACTUAL — EVIDENCIA DEL BUG

### 2.1 Punto de entrada: RefundRepository.ProcessAsync

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/RefundRepository.cs`  
**Líneas**: 187-373

```csharp
public async Task<RefundProcessResult> ProcessAsync(RefundProcessCommand command)
{
    // ... validaciones, cálculos financieros ...
    
    await RestoreInventoryAsync(connection, transaction, command, order, orderItems, refundItems, refund.Id);
    
    // ... ajustes de crédito, caja ...
}
```

**Línea crítica**: 341

### 2.2 Restitución de inventario: RestoreInventoryAsync

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/RefundRepository.cs`  
**Líneas**: 670-711

```csharp
private static async Task RestoreInventoryAsync(
    System.Data.IDbConnection connection,
    System.Data.IDbTransaction transaction,
    RefundProcessCommand command,
    RefundOrderRow order,
    List<RefundOrderItemRow> orderItems,
    List<RefundItem> refundItems,
    long refundId)
{
    foreach (var refundItem in refundItems)
    {
        var orderItem = orderItems.Single(i => i.Id == refundItem.OrderItemId);
        if (orderItem.ProductType == "prepared")
        {
            var ingredients = await connection.QueryAsync<RecipeRefundRow>(@"
                SELECT r.ingredient_id AS ProductId,
                       r.quantity AS QuantityPerProduct,
                       p.track_stock AS TrackStock
                FROM inventory.recipes r
                JOIN inventory.products p
                  ON p.id = r.ingredient_id AND p.company_id = r.company_id
                WHERE r.company_id = @CompanyId AND r.product_id = @ProductId", new
            {
                command.CompanyId,
                ProductId = orderItem.ProductId
            }, transaction);

            foreach (var ingredient in ingredients.Where(i => i.TrackStock))
            {
                await RestoreTrackedProductAsync(connection, transaction, command,
                    ingredient.ProductId, ingredient.QuantityPerProduct * refundItem.Quantity,
                    0, "refund_recipe", refundId, order.OrderNumber);
            }
        }
        else if (orderItem.TrackStock)
        {
            await RestoreTrackedProductAsync(connection, transaction, command,
                orderItem.ProductId, refundItem.Quantity, refundItem.UnitPrice,
                "refund", refundId, order.OrderNumber);
        }
    }
}
```

**HALLAZGO #4 — BUG CONFIRMADO**:

**Líneas 684-695**: El refund **RELEE LA RECETA ACTUAL** desde `inventory.recipes`:

```sql
SELECT r.ingredient_id AS ProductId,
       r.quantity AS QuantityPerProduct,
       p.track_stock AS TrackStock
FROM inventory.recipes r
JOIN inventory.products p
  ON p.id = r.ingredient_id AND p.company_id = r.company_id
WHERE r.company_id = @CompanyId AND r.product_id = @ProductId
```

**NO consulta**:
- Los movimientos originales de la venta
- Ningún snapshot histórico
- Ninguna tabla de versiones de receta

**Consecuencia**: Si la receta cambió entre la venta y el refund, se restituyen cantidades incorrectas.

### 2.3 Test que documenta el comportamiento actual

**Archivo**: `backend-dotnet/tests/Walos.Tests/Integration/RefundAtomicityIntegrationTests.cs`  
**Líneas**: 40-50

```csharp
[SkippableFact]
public async Task Prepared_Product_Partial_Refund_Restores_Current_Recipe_Ingredient()
{
    var ctx = await SeedPreparedOrderAsync("Prepared partial");

    var result = await RefundAsync(ctx, "partial", NewKey(), 0.5m);

    Assert.Equal(25m, result.RefundAmount);
    Assert.Equal(9m, await GetStockAsync(ctx));
    Assert.Equal("refund_recipe", await GetLastMovementTypeAsync(ctx));
    Assert.Equal(1m, await GetLastMovementQuantityAsync(ctx));
}
```

**HALLAZGO #5**: El nombre del test **"Restores_Current_Recipe_Ingredient"** documenta explícitamente que el sistema usa la receta vigente, no la histórica.

---

## 3. ANÁLISIS DE ESTRUCTURAS DE DATOS

### 3.1 Tabla sales.order_items

**Archivo**: `supabase/migrations/004_sales_tables.sql`  
**Líneas**: 73-85

```sql
CREATE TABLE IF NOT EXISTS sales.order_items (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    order_id        BIGINT NOT NULL REFERENCES sales.orders(id),
    product_id      BIGINT NOT NULL REFERENCES inventory.products(id),

    product_name    VARCHAR(200) NOT NULL,
    quantity        DECIMAL(18,2) NOT NULL DEFAULT 1,
    unit_price      DECIMAL(18,2) NOT NULL,
    subtotal        DECIMAL(18,2) GENERATED ALWAYS AS (quantity * unit_price) STORED,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**HALLAZGO #6**: `order_items` **NO tiene snapshot de ingredientes**. Solo registra el producto preparado vendido, no su expansión.

### 3.2 Tabla sales.refund_items

**Archivo**: `supabase/migrations/016_cash_registers.sql`  
**Líneas**: 149-158

```sql
CREATE TABLE IF NOT EXISTS sales.refund_items (
    id              BIGSERIAL PRIMARY KEY,
    refund_id       BIGINT NOT NULL REFERENCES sales.refunds(id) ON DELETE CASCADE,
    order_item_id   BIGINT NOT NULL REFERENCES sales.order_items(id),
    quantity        DECIMAL(18,2) NOT NULL,
    unit_price      DECIMAL(18,2) NOT NULL,
    subtotal        DECIMAL(18,2) NOT NULL,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**HALLAZGO #7**: `refund_items` **NO tiene snapshot de ingredientes**. Solo referencia el `order_item_id`.

### 3.3 Tabla inventory.recipes

**Archivo**: `supabase/migrations/010_recipes_table.sql`  
**Líneas**: 6-18

```sql
CREATE TABLE IF NOT EXISTS inventory.recipes (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    product_id          BIGINT NOT NULL REFERENCES inventory.products(id),   -- producto preparado
    ingredient_id       BIGINT NOT NULL REFERENCES inventory.products(id),   -- insumo
    quantity            DECIMAL(18, 4) NOT NULL CHECK (quantity > 0),
    unit_id             BIGINT REFERENCES inventory.units(id),
    notes               VARCHAR(300),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (product_id, ingredient_id)
);
```

**HALLAZGO #8**: `recipes` **NO tiene versionado histórico**. Es una tabla mutable que se actualiza in-place.

---

## 4. ESCENARIO DE REPRODUCCIÓN DEL BUG

### Configuración inicial

```
Producto preparado: "Hamburguesa Clásica" (ID: 100)
Receta V1:
  - Carne molida (ID: 10): 150g
  - Pan (ID: 20): 1 unidad
```

### Paso 1: Venta

**Acción**: Vender 2 hamburguesas

**Movimientos generados**:
```sql
INSERT INTO inventory.movements (
    product_id, movement_type, quantity, reference_type, reference_id, notes
) VALUES
    (10, 'recipe_consumption', -300, 'order', 5001, 'Consumo receta - Venta Mesa 3 - ORD-001'),
    (20, 'recipe_consumption', -2,   'order', 5001, 'Consumo receta - Venta Mesa 3 - ORD-001');
```

**Stock después**:
- Carne: 1000g → 700g
- Pan: 50 → 48

### Paso 2: Cambio de receta

**Acción**: Modificar receta

```
Receta V2:
  - Carne molida (ID: 10): 120g  ← CAMBIÓ
  - Pan (ID: 20): 1 unidad
  - Queso (ID: 30): 50g          ← NUEVO
```

### Paso 3: Refund

**Acción**: Devolver 1 hamburguesa

**Comportamiento actual (INCORRECTO)**:

El sistema relee la receta V2 y restituye:
```sql
-- Movimientos generados por el refund actual:
INSERT INTO inventory.movements (
    product_id, movement_type, quantity, reference_type, reference_id
) VALUES
    (10, 'refund_recipe', +120, 'refund', 8001),  ← INCORRECTO (debería ser +150)
    (20, 'refund_recipe', +1,   'refund', 8001),  ← CORRECTO
    (30, 'refund_recipe', +50,  'refund', 8001);  ← INCORRECTO (no se consumió en la venta)
```

**Stock después del refund**:
- Carne: 700g + 120g = 820g  ← **INCORRECTO** (debería ser 850g)
- Pan: 48 + 1 = 49           ← CORRECTO
- Queso: 200g + 50g = 250g   ← **INCORRECTO** (debería seguir en 200g)

**Comportamiento esperado (CORRECTO)**:

Reversar los movimientos originales:
```sql
-- Movimientos que DEBERÍAN generarse:
INSERT INTO inventory.movements (
    product_id, movement_type, quantity, reference_type, reference_id
) VALUES
    (10, 'refund_recipe', +150, 'refund', 8001),  ← Reversa el movimiento original
    (20, 'refund_recipe', +1,   'refund', 8001);  ← Reversa el movimiento original
```

**Stock correcto después del refund**:
- Carne: 700g + 150g = 850g  ← CORRECTO
- Pan: 48 + 1 = 49           ← CORRECTO
- Queso: 200g                ← CORRECTO (sin cambios)

---

## 5. COMPARACIÓN DE ALTERNATIVAS ARQUITECTÓNICAS

### 5.1 Opción 1: Versionado de recetas

#### Descripción
Persistir versiones completas de recetas con timestamp. Cada venta referencia la versión vigente.

#### Esquema propuesto
```sql
CREATE TABLE inventory.recipe_versions (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL,
    product_id      BIGINT NOT NULL,
    version_number  INT NOT NULL,
    valid_from      TIMESTAMPTZ NOT NULL,
    valid_until     TIMESTAMPTZ,
    created_by      BIGINT,
    UNIQUE (product_id, version_number)
);

CREATE TABLE inventory.recipe_version_ingredients (
    version_id      BIGINT NOT NULL REFERENCES inventory.recipe_versions(id),
    ingredient_id   BIGINT NOT NULL,
    quantity        DECIMAL(18, 4) NOT NULL,
    unit_id         BIGINT,
    PRIMARY KEY (version_id, ingredient_id)
);

ALTER TABLE sales.order_items
    ADD COLUMN recipe_version_id BIGINT REFERENCES inventory.recipe_versions(id);
```

#### Evaluación

| Criterio | Valoración | Justificación |
|----------|------------|---------------|
| **Precisión histórica** | ✅ Alta | Preserva recetas exactas |
| **Complejidad** | ⚠️ Alta | Requiere gestión de versiones, migraciones complejas |
| **Storage** | ⚠️ Medio-Alto | Duplica datos de recetas por cada versión |
| **Queries** | ⚠️ Complejo | Joins adicionales en venta y refund |
| **Compatibilidad legacy** | ❌ Difícil | Ventas antiguas no tienen `recipe_version_id` |
| **Refunds parciales** | ✅ Soportado | Referencia directa a versión |
| **Utilidad adicional** | ✅ Alta | Permite auditoría de cambios de receta |
| **Mantenimiento** | ⚠️ Complejo | Requiere lógica de versionado en updates |

#### Riesgos
- **Migración de datos históricos**: Ventas antiguas no tienen versión asignada
- **Complejidad operativa**: Cada cambio de receta crea nueva versión
- **Overhead de storage**: Recetas con cambios frecuentes generan muchas versiones

---

### 5.2 Opción 2: Snapshot de consumo por venta

#### Descripción
Persistir en `order_items` o tabla relacionada los ingredientes exactos consumidos al momento de la venta.

#### Esquema propuesto
```sql
CREATE TABLE sales.order_item_ingredients (
    id              BIGSERIAL PRIMARY KEY,
    order_item_id   BIGINT NOT NULL REFERENCES sales.order_items(id),
    ingredient_id   BIGINT NOT NULL REFERENCES inventory.products(id),
    quantity        DECIMAL(18, 4) NOT NULL,
    unit_cost       DECIMAL(18, 2),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (order_item_id, ingredient_id)
);

CREATE INDEX idx_order_item_ingredients_item ON sales.order_item_ingredients (order_item_id);
```

#### Evaluación

| Criterio | Valoración | Justificación |
|----------|------------|---------------|
| **Precisión histórica** | ✅ Alta | Snapshot exacto del consumo |
| **Complejidad** | ⚠️ Media | Requiere nueva tabla y lógica de escritura |
| **Storage** | ⚠️ Medio | Duplica info ya en `inventory.movements` |
| **Queries** | ✅ Simple | Join directo desde `order_items` |
| **Compatibilidad legacy** | ⚠️ Media | Ventas antiguas no tienen snapshot, requiere fallback |
| **Refunds parciales** | ✅ Soportado | Proporcional al snapshot |
| **Utilidad adicional** | ⚠️ Limitada | Solo útil para refunds |
| **Mantenimiento** | ✅ Simple | Escritura automática en venta |

#### Riesgos
- **Duplicación de datos**: La misma info ya está en `inventory.movements`
- **Inconsistencia potencial**: Dos fuentes de verdad (snapshot vs movements)
- **Migración legacy**: Ventas antiguas sin snapshot requieren fallback a receta actual (perpetúa el bug)

---

### 5.3 Opción 3: Reversión de movimientos originales ⭐ RECOMENDADA

#### Descripción
Consultar `inventory.movements` con `reference_type='order'` y `reference_id={order_id}` para obtener los movimientos exactos de la venta, y crear movimientos inversos.

#### Cambios requeridos

**Modificación en RefundRepository.RestoreInventoryAsync**:

```csharp
private static async Task RestoreInventoryAsync(
    System.Data.IDbConnection connection,
    System.Data.IDbTransaction transaction,
    RefundProcessCommand command,
    RefundOrderRow order,
    List<RefundOrderItemRow> orderItems,
    List<RefundItem> refundItems,
    long refundId)
{
    foreach (var refundItem in refundItems)
    {
        var orderItem = orderItems.Single(i => i.Id == refundItem.OrderItemId);
        
        if (orderItem.ProductType == "prepared")
        {
            // NUEVO: Consultar movimientos originales en lugar de receta actual
            var originalMovements = await connection.QueryAsync<OriginalMovementRow>(@"
                SELECT product_id AS ProductId,
                       ABS(quantity) AS Quantity,
                       unit_cost AS UnitCost
                FROM inventory.movements
                WHERE company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND reference_type = 'order'
                  AND reference_id = @OrderId
                  AND movement_type = 'recipe_consumption'
                  AND quantity < 0
                ORDER BY product_id", new
            {
                command.CompanyId,
                command.BranchId,
                OrderId = order.Id
            }, transaction);

            if (!originalMovements.Any())
            {
                // Fallback para ventas legacy sin movimientos detallados
                // (usar receta actual como último recurso)
                originalMovements = await GetCurrentRecipeAsFallback(connection, transaction, command, orderItem.ProductId);
            }

            // Calcular proporción para refund parcial
            var refundRatio = refundItem.Quantity / orderItem.Quantity;

            foreach (var movement in originalMovements.Where(m => m.TrackStock))
            {
                await RestoreTrackedProductAsync(connection, transaction, command,
                    movement.ProductId,
                    movement.Quantity * refundRatio,
                    movement.UnitCost,
                    "refund_recipe",
                    refundId,
                    order.OrderNumber);
            }
        }
        else if (orderItem.TrackStock)
        {
            // Productos simples: consultar movimiento original
            var originalMovement = await connection.QuerySingleOrDefaultAsync<OriginalMovementRow>(@"
                SELECT product_id AS ProductId,
                       ABS(quantity) AS Quantity,
                       unit_cost AS UnitCost
                FROM inventory.movements
                WHERE company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND reference_type = 'order'
                  AND reference_id = @OrderId
                  AND movement_type = 'sale'
                  AND product_id = @ProductId
                  AND quantity < 0
                ORDER BY created_at DESC
                LIMIT 1", new
            {
                command.CompanyId,
                command.BranchId,
                OrderId = order.Id,
                ProductId = orderItem.ProductId
            }, transaction);

            if (originalMovement is not null)
            {
                var refundRatio = refundItem.Quantity / orderItem.Quantity;
                await RestoreTrackedProductAsync(connection, transaction, command,
                    originalMovement.ProductId,
                    originalMovement.Quantity * refundRatio,
                    originalMovement.UnitCost,
                    "refund",
                    refundId,
                    order.OrderNumber);
            }
        }
    }
}
```

#### Evaluación

| Criterio | Valoración | Justificación |
|----------|------------|---------------|
| **Precisión histórica** | ✅ Máxima | Usa movimientos reales de la venta |
| **Complejidad** | ✅ Baja | Solo modifica lógica de refund, sin cambios de schema |
| **Storage** | ✅ Cero adicional | Reutiliza datos existentes |
| **Queries** | ✅ Simple | Index existente `(reference_type, reference_id)` |
| **Compatibilidad legacy** | ✅ Alta | Fallback a receta actual para ventas sin movimientos |
| **Refunds parciales** | ✅ Soportado | Proporcional a movimientos originales |
| **Utilidad adicional** | ✅ Máxima | Aprovecha ledger ya existente |
| **Mantenimiento** | ✅ Simple | Sin nuevas estructuras ni versionado |
| **Atomicidad** | ✅ Garantizada | Transacción única |
| **Idempotencia** | ✅ Preservada | Usa mismo mecanismo actual |

#### Ventajas específicas

1. **Sin cambios de schema**: No requiere migraciones de base de datos
2. **Reutiliza infraestructura**: El ledger ya existe y está indexado
3. **Compatibilidad total**: Fallback para ventas legacy
4. **Refunds múltiples**: Soporta múltiples refunds parciales de la misma orden
5. **Unidades y conversiones**: Preserva `unit_cost` original
6. **Auditoría completa**: Trazabilidad de movimiento original → refund

#### Riesgos mitigados

| Riesgo | Mitigación |
|--------|------------|
| Ventas legacy sin movimientos | Fallback a receta actual (comportamiento actual) |
| Múltiples refunds parciales | Ratio proporcional a cantidad devuelta |
| Productos con múltiples movimientos | `ORDER BY created_at DESC LIMIT 1` |
| Cambios en `unit_cost` | Usa costo histórico del movimiento |

---

## 6. ANÁLISIS DE COMPATIBILIDAD CON VENTAS LEGACY

### 6.1 Definición de ventas legacy

**Ventas legacy**: Órdenes completadas antes de la implementación de la corrección, que tienen movimientos de inventario registrados.

**Identificación**:
```sql
SELECT COUNT(*) AS ventas_con_movimientos
FROM sales.orders o
WHERE o.status = 'completed'
  AND EXISTS (
      SELECT 1
      FROM inventory.movements m
      WHERE m.reference_type = 'order'
        AND m.reference_id = o.id
  );
```

### 6.2 Estrategia de compatibilidad

#### Para ventas CON movimientos registrados (mayoría esperada)

**Comportamiento**: Reversión normal de movimientos originales.

**Validación**:
```csharp
var originalMovements = await connection.QueryAsync<OriginalMovementRow>(@"
    SELECT product_id AS ProductId,
           ABS(quantity) AS Quantity,
           unit_cost AS UnitCost
    FROM inventory.movements
    WHERE company_id = @CompanyId
      AND branch_id = @BranchId
      AND reference_type = 'order'
      AND reference_id = @OrderId
      AND movement_type IN ('sale', 'recipe_consumption')
      AND quantity < 0
    ORDER BY product_id", ...);

if (originalMovements.Any())
{
    // Reversión histórica correcta
    foreach (var movement in originalMovements)
    {
        await RestoreTrackedProductAsync(..., movement.Quantity * refundRatio, ...);
    }
}
```

#### Para ventas SIN movimientos registrados (casos edge)

**Escenarios posibles**:
1. Ventas de productos con `track_stock = false`
2. Ventas anteriores a la implementación del ledger (poco probable)
3. Corrupción de datos (requiere investigación)

**Fallback**:
```csharp
if (!originalMovements.Any())
{
    _logger.LogWarning(
        "No se encontraron movimientos originales para order {OrderId}. " +
        "Usando receta actual como fallback.",
        order.Id);

    // Comportamiento actual (receta vigente)
    var currentRecipe = await connection.QueryAsync<RecipeRefundRow>(@"
        SELECT r.ingredient_id AS ProductId,
               r.quantity AS QuantityPerProduct,
               p.track_stock AS TrackStock,
               p.cost_price AS UnitCost
        FROM inventory.recipes r
        JOIN inventory.products p
          ON p.id = r.ingredient_id AND p.company_id = r.company_id
        WHERE r.company_id = @CompanyId AND r.product_id = @ProductId", ...);

    foreach (var ingredient in currentRecipe.Where(i => i.TrackStock))
    {
        await RestoreTrackedProductAsync(..., ingredient.QuantityPerProduct * refundItem.Quantity, ...);
    }
}
```

**Logging y monitoreo**:
```csharp
await connection.ExecuteAsync(@"
    INSERT INTO audit.refund_fallbacks (
        refund_id, order_id, reason, created_at
    ) VALUES (
        @RefundId, @OrderId, 'no_original_movements', NOW()
    )", new { RefundId = refundId, OrderId = order.Id }, transaction);
```

### 6.3 Migración de datos históricos (opcional)

**NO requerida** para la implementación, pero útil para auditoría:

```sql
-- Identificar órdenes sin movimientos de inventario
SELECT o.id AS order_id,
       o.order_number,
       o.created_at,
       oi.product_id,
       oi.product_name,
       oi.quantity,
       p.product_type
FROM sales.orders o
JOIN sales.order_items oi ON oi.order_id = o.id
JOIN inventory.products p ON p.id = oi.product_id
WHERE o.status = 'completed'
  AND o.created_at >= '2024-01-01'
  AND NOT EXISTS (
      SELECT 1
      FROM inventory.movements m
      WHERE m.reference_type = 'order'
        AND m.reference_id = o.id
  )
ORDER BY o.created_at DESC;
```

---

## 7. ANÁLISIS DE REFUNDS PARCIALES Y MÚLTIPLES

### 7.1 Refund parcial único

**Escenario**: Venta de 4 hamburguesas, refund de 1.

**Movimientos originales**:
```sql
-- Venta de 4 hamburguesas (receta: 150g carne + 1 pan)
INSERT INTO inventory.movements (product_id, movement_type, quantity, reference_id)
VALUES
    (10, 'recipe_consumption', -600, 5001),  -- 4 × 150g carne
    (20, 'recipe_consumption', -4,   5001);  -- 4 × 1 pan
```

**Refund de 1 hamburguesa**:

```csharp
var refundRatio = 1m / 4m;  // 0.25

// Movimientos de refund:
// Carne: 600g × 0.25 = 150g
// Pan: 4 × 0.25 = 1
```

**Resultado**:
```sql
INSERT INTO inventory.movements (product_id, movement_type, quantity, reference_id)
VALUES
    (10, 'refund_recipe', +150, 8001),
    (20, 'refund_recipe', +1,   8001);
```

✅ **Correcto**: Restituye exactamente lo consumido por 1 hamburguesa.

### 7.2 Múltiples refunds parciales

**Escenario**: Venta de 4 hamburguesas, refund de 1, luego refund de 2 más.

**Primer refund (1 hamburguesa)**:
```csharp
var refundRatio1 = 1m / 4m;  // 0.25
// Restituye: 150g carne, 1 pan
```

**Segundo refund (2 hamburguesas)**:
```csharp
var refundRatio2 = 2m / 4m;  // 0.50
// Restituye: 300g carne, 2 panes
```

**Total restituido**: 450g carne, 3 panes (de 600g y 4 panes vendidos)

**Validación de límites** (código actual):
```csharp
var available = item.Quantity - item.RefundedQuantity;
if (requested.Quantity > available)
    throw new ValidationException($"Cantidad invalida para {item.ProductName}. Disponible: {available}");
```

✅ **Soportado**: El sistema actual ya valida que la suma de refunds no exceda la cantidad vendida.

### 7.3 Refund completo después de cambio de receta

**Escenario**: Venta de 2 hamburguesas (receta V1), cambio a receta V2, refund completo.

**Movimientos originales (receta V1: 150g carne + 1 pan)**:
```sql
INSERT INTO inventory.movements (product_id, movement_type, quantity, reference_id)
VALUES
    (10, 'recipe_consumption', -300, 5001),
    (20, 'recipe_consumption', -2,   5001);
```

**Receta V2 (120g carne + 1 pan + 50g queso)**: Irrelevante para el refund.

**Refund completo (2 hamburguesas)**:
```csharp
var refundRatio = 2m / 2m;  // 1.0

// Consulta movimientos originales:
// Carne: 300g
// Pan: 2
```

**Resultado**:
```sql
INSERT INTO inventory.movements (product_id, movement_type, quantity, reference_id)
VALUES
    (10, 'refund_recipe', +300, 8001),
    (20, 'refund_recipe', +2,   8001);
```

✅ **Correcto**: Restituye exactamente lo consumido, ignorando la receta V2.

---

## 8. IMPACTO Y ESTIMACIÓN

### 8.1 Cambios de código requeridos

| Archivo | Tipo de cambio | Líneas estimadas |
|---------|----------------|------------------|
| `RefundRepository.cs` | Modificación de `RestoreInventoryAsync` | ~80 líneas |
| `RefundRepository.cs` | Nuevo método `GetCurrentRecipeAsFallback` | ~30 líneas |
| `RefundAtomicityIntegrationTests.cs` | Actualizar tests existentes | ~20 líneas |
| `RefundAtomicityIntegrationTests.cs` | Nuevos tests para reversión | ~100 líneas |

**Total estimado**: ~230 líneas de código.

### 8.2 Cambios de schema

**Ninguno requerido**. La solución reutiliza estructuras existentes.

### 8.3 Riesgos técnicos

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| Ventas sin movimientos | Baja | Medio | Fallback a receta actual + logging |
| Performance de query | Baja | Bajo | Index existente `(reference_type, reference_id)` |
| Refunds duplicados | Muy baja | Alto | Idempotencia ya implementada |
| Corrupción de movimientos | Muy baja | Alto | Validación en tests de integración |

### 8.4 Plan de rollout

1. **Desarrollo y tests** (1-2 días)
   - Implementar cambios en `RefundRepository`
   - Actualizar tests existentes
   - Crear tests para casos edge

2. **QA en staging** (1 día)
   - Verificar refunds de productos preparados
   - Verificar refunds parciales
   - Verificar fallback para ventas legacy

3. **Deploy a producción** (1 hora)
   - Sin downtime (solo cambio de código)
   - Sin migraciones de schema
   - Monitoreo de logs de fallback

4. **Monitoreo post-deploy** (1 semana)
   - Alertas en logs de fallback
   - Validación de integridad de stock
   - Revisión de refunds procesados

---

## 9. CONCLUSIÓN Y RECOMENDACIÓN FINAL

### 9.1 Resumen de hallazgos

1. ✅ **El ledger de inventario es completo**: Registra todos los movimientos con `reference_type`, `reference_id`, `product_id`, `quantity`, `unit_cost` y `created_at`.

2. ❌ **El refund actual tiene un bug arquitectónico**: Relee la receta vigente en lugar de reversar movimientos originales.

3. ✅ **La infraestructura para la solución ya existe**: Index `(reference_type, reference_id)` permite consultas eficientes.

4. ✅ **La solución es compatible con ventas legacy**: Fallback a receta actual para casos edge.

5. ✅ **La solución soporta refunds parciales y múltiples**: Ratio proporcional a cantidad devuelta.

### 9.2 Recomendación arquitectónica

## ⭐ RECOMENDADO: REVERSIÓN DE MOVIMIENTOS ORIGINALES

**Justificación**:

1. **Precisión histórica máxima**: Usa los movimientos reales de la venta, no una aproximación.

2. **Cero cambios de schema**: No requiere migraciones, versionado ni nuevas tablas.

3. **Reutiliza infraestructura existente**: El ledger ya tiene toda la información necesaria.

4. **Compatibilidad total**: Fallback para ventas legacy sin romper funcionalidad actual.

5. **Simplicidad de implementación**: ~230 líneas de código, sin complejidad adicional.

6. **Mantenimiento mínimo**: Sin nuevas estructuras que mantener o versionar.

7. **Auditoría completa**: Trazabilidad de movimiento original → refund.

8. **Soporta todos los casos**: Refunds parciales, múltiples, productos simples y preparados.

### 9.3 Alternativas descartadas

- **Versionado de recetas**: Complejidad alta, storage adicional, migración compleja.
- **Snapshot de consumo**: Duplicación de datos, dos fuentes de verdad, migración legacy.

### 9.4 Próximos pasos

1. Implementar cambios en `RefundRepository.RestoreInventoryAsync`
2. Actualizar tests de integración
3. Validar en staging con casos reales
4. Deploy a producción sin downtime
5. Monitorear logs de fallback durante 1 semana

---

## ANEXO A: QUERIES DE VALIDACIÓN

### A.1 Verificar movimientos de una orden

```sql
SELECT m.id,
       m.product_id,
       p.name AS product_name,
       m.movement_type,
       m.quantity,
       m.unit_cost,
       m.reference_type,
       m.reference_id,
       m.notes,
       m.created_at
FROM inventory.movements m
JOIN inventory.products p ON p.id = m.product_id
WHERE m.reference_type = 'order'
  AND m.reference_id = :order_id
ORDER BY m.created_at;
```

### A.2 Verificar refunds de una orden

```sql
SELECT r.id AS refund_id,
       r.refund_type,
       r.refund_amount,
       ri.order_item_id,
       ri.quantity AS refunded_quantity,
       oi.product_name,
       oi.quantity AS original_quantity
FROM sales.refunds r
JOIN sales.refund_items ri ON ri.refund_id = r.id
JOIN sales.order_items oi ON oi.id = ri.order_item_id
WHERE r.order_id = :order_id
ORDER BY r.created_at;
```

### A.3 Auditar integridad de stock después de refund

```sql
WITH sale_movements AS (
    SELECT product_id,
           SUM(quantity) AS total_sold
    FROM inventory.movements
    WHERE reference_type = 'order'
      AND reference_id = :order_id
      AND movement_type IN ('sale', 'recipe_consumption')
    GROUP BY product_id
),
refund_movements AS (
    SELECT product_id,
           SUM(quantity) AS total_refunded
    FROM inventory.movements
    WHERE reference_type = 'refund'
      AND reference_id IN (
          SELECT id FROM sales.refunds WHERE order_id = :order_id
      )
      AND movement_type IN ('refund', 'refund_recipe')
    GROUP BY product_id
)
SELECT s.product_id,
       p.name,
       s.total_sold,
       COALESCE(r.total_refunded, 0) AS total_refunded,
       s.total_sold + COALESCE(r.total_refunded, 0) AS net_impact
FROM sale_movements s
LEFT JOIN refund_movements r ON r.product_id = s.product_id
JOIN inventory.products p ON p.id = s.product_id
ORDER BY s.product_id;
```

---

## ANEXO B: TESTS PROPUESTOS

### B.1 Test: Refund después de cambio de receta

```csharp
[Fact]
public async Task Refund_After_Recipe_Change_Restores_Original_Ingredients()
{
    // Arrange: Crear producto preparado con receta V1
    var prepared = await SeedPreparedProductAsync("Hamburguesa", new[]
    {
        (IngredientId: 10, Quantity: 150m),  // Carne
        (IngredientId: 20, Quantity: 1m)     // Pan
    });

    // Vender 2 hamburguesas
    var order = await SeedOrderWithPreparedAsync(prepared.ProductId, quantity: 2m);

    // Cambiar receta a V2
    await UpdateRecipeAsync(prepared.ProductId, new[]
    {
        (IngredientId: 10, Quantity: 120m),  // Carne (cambió)
        (IngredientId: 20, Quantity: 1m),    // Pan
        (IngredientId: 30, Quantity: 50m)    // Queso (nuevo)
    });

    // Act: Refund completo
    await RefundAsync(order.Id, "full", NewKey());

    // Assert: Debe restituir receta V1, no V2
    var movements = await GetRefundMovementsAsync(order.Id);
    Assert.Equal(2, movements.Count);
    Assert.Contains(movements, m => m.ProductId == 10 && m.Quantity == 300m);  // 2 × 150g
    Assert.Contains(movements, m => m.ProductId == 20 && m.Quantity == 2m);    // 2 × 1
    Assert.DoesNotContain(movements, m => m.ProductId == 30);  // Queso NO debe restituirse
}
```

### B.2 Test: Refund parcial proporcional

```csharp
[Fact]
public async Task Partial_Refund_Restores_Proportional_Ingredients()
{
    // Arrange: Vender 4 hamburguesas (receta: 150g carne + 1 pan)
    var order = await SeedOrderWithPreparedAsync(preparedId: 100, quantity: 4m);

    // Act: Refund de 1 hamburguesa
    await RefundAsync(order.Id, "partial", NewKey(), quantity: 1m);

    // Assert: Debe restituir 1/4 de los ingredientes
    var movements = await GetRefundMovementsAsync(order.Id);
    Assert.Contains(movements, m => m.ProductId == 10 && m.Quantity == 150m);  // 600g × 0.25
    Assert.Contains(movements, m => m.ProductId == 20 && m.Quantity == 1m);    // 4 × 0.25
}
```

### B.3 Test: Fallback para ventas legacy

```csharp
[Fact]
public async Task Refund_Without_Original_Movements_Uses_Current_Recipe_Fallback()
{
    // Arrange: Simular venta legacy sin movimientos de inventario
    var order = await SeedLegacyOrderAsync(preparedId: 100, quantity: 2m);

    // Act: Refund
    await RefundAsync(order.Id, "full", NewKey());

    // Assert: Debe usar receta actual como fallback
    var movements = await GetRefundMovementsAsync(order.Id);
    Assert.NotEmpty(movements);

    // Verificar que se logueó el fallback
    var logs = await GetRefundLogsAsync(order.Id);
    Assert.Contains(logs, l => l.Message.Contains("fallback"));
}
```

---

**FIN DEL INFORME**

---

**Firmado**: Auditor Arquitectónico  
**Fecha**: 2025-01-15  
**Versión**: 1.0
