# SOLUCIÓN FINAL — REFUND DE PREPARADOS CON `source_order_item_id`

> **Fecha**: 2025-01-15  
> **Auditor**: Arquitectónico independiente  
> **Versión**: 2.0 (Revisión crítica)  
> **Estado**: Recomendación final

---

## RESUMEN EJECUTIVO

**Problema**: Refunds de productos preparados restituyen cantidades incorrectas cuando la receta cambia post-venta.

**Causa raíz**: Movimientos `recipe_consumption` se agrupan por ingrediente a nivel de orden completa, perdiendo correlación con `order_item_id`.

**Solución**: Agregar columna `source_order_item_id` a `inventory.movements` y emitir movimientos desagregados por item.

**Impacto**:
- ✅ Schema: 1 columna + 1 FK + 1 índice
- ✅ Código: ~400 líneas en 5 archivos
- ✅ Storage: +10-20% filas en `movements`
- ✅ Tiempo: 3-4 días

---

## PARTE 1: MIGRACIÓN DE SCHEMA

### Archivo: `supabase/migrations/019_add_source_order_item_to_movements.sql`

```sql
-- ============================================================
-- 019: Agregar source_order_item_id a inventory.movements
-- Propósito: Correlacionar movimientos recipe_consumption con order_item específico
-- Fecha: 2025-01-15
-- ============================================================

-- 1. Agregar columna (nullable para compatibilidad con datos existentes)
ALTER TABLE inventory.movements
    ADD COLUMN source_order_item_id BIGINT;

-- 2. Agregar foreign key constraint
ALTER TABLE inventory.movements
    ADD CONSTRAINT fk_movements_source_order_item
    FOREIGN KEY (source_order_item_id)
    REFERENCES sales.order_items(id)
    ON DELETE SET NULL;

-- 3. Crear índice para queries de refund
CREATE INDEX idx_inv_movements_source_item 
    ON inventory.movements (source_order_item_id)
    WHERE source_order_item_id IS NOT NULL;

-- 4. Comentario de documentación
COMMENT ON COLUMN inventory.movements.source_order_item_id IS
    'ID del order_item que originó este movimiento (para recipe_consumption y sale). ' ||
    'NULL para movimientos legacy (pre-2025-01-15) o no aplicables (ajustes, transferencias).';

-- 5. Validación post-migración
DO $$
BEGIN
    -- Verificar que la columna existe
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'inventory'
          AND table_name = 'movements'
          AND column_name = 'source_order_item_id'
    ) THEN
        RAISE EXCEPTION 'Migración fallida: columna source_order_item_id no existe';
    END IF;
    
    -- Verificar que el índice existe
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'inventory'
          AND tablename = 'movements'
          AND indexname = 'idx_inv_movements_source_item'
    ) THEN
        RAISE EXCEPTION 'Migración fallida: índice idx_inv_movements_source_item no existe';
    END IF;
    
    RAISE NOTICE 'Migración 019 completada exitosamente';
END $$;
```

---

## PARTE 2: MODIFICACIONES DE CÓDIGO

### 2.1 Domain Layer: `InventoryMovementPlanner.cs`

**Archivo**: `backend-dotnet/src/Walos.Domain/Policies/InventoryMovementPlanner.cs`

```csharp
using Walos.Domain.Exceptions;

namespace Walos.Domain.Policies;

public enum InventoryMovementDirection
{
    Inbound,
    Outbound
}

// MODIFICADO: Agregar SourceOrderItemId
public sealed record InventoryMovementPlan(
    InventoryMovementDirection Direction,
    long ProductId,
    string MovementType,
    decimal Quantity,
    decimal UnitCost,
    string ReferenceType,
    long ReferenceId,
    bool RequiresStock,
    string? Notes,
    decimal? StockAfter,
    long? SourceOrderItemId);  // ← NUEVO

public static class InventoryMovementPlanner
{
    public const int InventoryQuantityDecimals = 3;

    // MODIFICADO: Agregar parámetro sourceOrderItemId
    public static InventoryMovementPlan Create(
        InventoryMovementDirection direction,
        long productId,
        string movementType,
        decimal quantity,
        decimal unitCost,
        string referenceType,
        long referenceId,
        bool requiresStock,
        string? notes = null,
        long? sourceOrderItemId = null)  // ← NUEVO
    {
        if (productId <= 0)
            throw new ValidationException("Producto requerido para el movimiento");
        var normalizedQuantity = NormalizeQuantity(quantity);
        if (normalizedQuantity <= 0)
            throw new ValidationException("La cantidad del movimiento debe ser mayor que cero");
        if (unitCost < 0)
            throw new ValidationException("El costo del movimiento no puede ser negativo");
        if (string.IsNullOrWhiteSpace(movementType))
            throw new ValidationException("Tipo de movimiento requerido");
        if (string.IsNullOrWhiteSpace(referenceType) || referenceId <= 0)
            throw new ValidationException("Referencia de movimiento requerida");

        return new InventoryMovementPlan(
            direction,
            productId,
            movementType.Trim(),
            normalizedQuantity,
            PaymentPolicy.RoundMoney(unitCost),
            referenceType.Trim(),
            referenceId,
            requiresStock,
            notes?.Trim(),
            StockAfter: null,
            sourceOrderItemId);  // ← NUEVO
    }

    public static InventoryMovementPlan WithStockAfter(InventoryMovementPlan plan, decimal stockAfter) =>
        plan with { StockAfter = stockAfter };

    public static decimal NormalizeQuantity(decimal quantity) =>
        Math.Round(quantity, InventoryQuantityDecimals, MidpointRounding.AwayFromZero);
}
```

---

### 2.2 Infrastructure Layer: `SaleInventoryPlanBuilder.cs`

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Inventory/SaleInventoryPlanBuilder.cs`

```csharp
using System.Data;
using Dapper;
using Walos.Domain.Exceptions;
using Walos.Domain.Policies;

namespace Walos.Infrastructure.Inventory;

// MODIFICADO: Agregar OrderItemId
public sealed record SaleInventoryLine(
    long ProductId,
    string ProductName,
    string ProductType,
    decimal Quantity,
    decimal UnitCost,
    bool TrackStock,
    bool ProductExists,
    bool IsActive,
    bool IsForSale,
    long OrderItemId);  // ← NUEVO

public sealed record SaleInventoryPlanContext(
    long CompanyId,
    string ReferenceType,
    long ReferenceId,
    string SaleNotes,
    string RecipeNotes);

public sealed class SaleInventoryPlanBuilder
{
    public async Task<IReadOnlyList<InventoryMovementPlan>> BuildAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        SaleInventoryPlanContext context,
        IReadOnlyCollection<SaleInventoryLine> lines)
    {
        if (lines.Count == 0)
            throw new BusinessException("La venta no contiene productos para inventario");

        foreach (var line in lines)
        {
            if (!SaleItemPolicy.IsQuantitySupported(line.Quantity) || line.UnitCost < 0)
                throw new ValidationException($"El producto {line.ProductId} tiene cantidad o costo invalido");
            if (!line.ProductExists)
                throw new ValidationException($"El producto {line.ProductId} no existe en el comercio");
            if (!line.IsActive || !line.IsForSale)
                throw new ValidationException($"El producto {line.ProductName} ya no esta disponible para venta");
        }

        // MODIFICADO: Agregar sourceOrderItemId para productos simples
        var plans = lines
            .Where(line => !IsPrepared(line.ProductType))
            .GroupBy(line => new { line.ProductId, line.TrackStock, line.UnitCost })
            .Select(group => InventoryMovementPlanner.Create(
                InventoryMovementDirection.Outbound,
                group.Key.ProductId,
                movementType: "sale",
                quantity: group.Sum(line => line.Quantity),
                unitCost: group.Key.UnitCost,
                referenceType: context.ReferenceType,
                referenceId: context.ReferenceId,
                requiresStock: group.Key.TrackStock,
                notes: context.SaleNotes,
                sourceOrderItemId: group.Count() == 1 ? group.First().OrderItemId : null))  // ← NUEVO
            .ToList();

        var preparedIds = lines
            .Where(line => IsPrepared(line.ProductType))
            .Select(line => line.ProductId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (preparedIds.Length == 0)
            return plans;

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

        foreach (var preparedId in preparedIds)
        {
            if (!recipes.Any(recipe => recipe.ProductId == preparedId))
                throw new BusinessException($"El producto preparado {preparedId} no tiene una receta valida");
        }

        if (recipes.Any(recipe => recipe.Quantity <= 0 || !recipe.IsActive || recipe.DeletedAt.HasValue))
            throw new BusinessException("La receta contiene ingredientes invalidos o inactivos");

        // MODIFICADO: NO agrupar, emitir un movimiento por ingrediente por item
        var preparedLines = lines.Where(line => IsPrepared(line.ProductType)).ToList();

        foreach (var preparedLine in preparedLines)
        {
            var itemRecipes = recipes.Where(r => r.ProductId == preparedLine.ProductId).ToList();

            foreach (var recipe in itemRecipes)
            {
                plans.Add(InventoryMovementPlanner.Create(
                    InventoryMovementDirection.Outbound,
                    recipe.IngredientId,
                    movementType: "recipe_consumption",
                    quantity: recipe.Quantity * preparedLine.Quantity,
                    unitCost: recipe.UnitCost,
                    referenceType: context.ReferenceType,
                    referenceId: context.ReferenceId,
                    requiresStock: recipe.TrackStock,
                    notes: context.RecipeNotes,
                    sourceOrderItemId: preparedLine.OrderItemId));  // ← NUEVO
            }
        }

        return plans;
    }

    private static bool IsPrepared(string? productType) =>
        string.Equals(productType?.Trim(), "prepared", StringComparison.OrdinalIgnoreCase);

    private sealed class RecipeRequirementRow
    {
        public long ProductId { get; init; }
        public long IngredientId { get; init; }
        public decimal Quantity { get; init; }
        public string IngredientName { get; init; } = string.Empty;
        public bool TrackStock { get; init; }
        public decimal UnitCost { get; init; }
        public bool IsActive { get; init; }
        public DateTime? DeletedAt { get; init; }
    }
}
```

---

### 2.3 Infrastructure Layer: `InventoryTransactionWriter.cs`

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Inventory/InventoryTransactionWriter.cs`

```csharp
using System.Data;
using Dapper;
using Walos.Domain.Exceptions;
using Walos.Domain.Policies;

namespace Walos.Infrastructure.Inventory;

public sealed record InventoryTransactionContext(
    long CompanyId,
    long BranchId,
    long UserId,
    long? ExcludedCommittedOrderId = null);

public sealed class InventoryTransactionWriter
{
    public async Task ApplyAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        InventoryTransactionContext context,
        IReadOnlyCollection<InventoryMovementPlan> plans)
    {
        if (plans.Count == 0)
            return;
        if (plans.Any(plan => plan.Direction != InventoryMovementDirection.Outbound))
            throw new ValidationException("El writer transaccional de ventas solo admite salidas de inventario");

        var orderedPlans = plans
            .OrderBy(plan => plan.ProductId)
            .ThenBy(plan => plan.MovementType, StringComparer.Ordinal)
            .ToList();
        var requirements = orderedPlans
            .Where(plan => plan.RequiresStock)
            .GroupBy(plan => plan.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(plan => plan.Quantity));

        if (requirements.Count > 0)
            await LockAndValidateAvailabilityAsync(connection, transaction, context, requirements);

        foreach (var plan in orderedPlans)
        {
            decimal? stockAfter = null;
            if (plan.RequiresStock)
            {
                stockAfter = await connection.QuerySingleOrDefaultAsync<decimal?>(@"
                    UPDATE inventory.stock
                    SET quantity = quantity - @Quantity,
                        updated_at = NOW()
                    WHERE company_id = @CompanyId
                      AND branch_id = @BranchId
                      AND product_id = @ProductId
                      AND quantity >= @Quantity
                    RETURNING quantity",
                    new
                    {
                        context.CompanyId,
                        context.BranchId,
                        plan.ProductId,
                        plan.Quantity
                    }, transaction);

                if (stockAfter is null)
                    throw new BusinessException($"Stock insuficiente para el producto {plan.ProductId}");
            }

            var completedPlan = stockAfter.HasValue
                ? InventoryMovementPlanner.WithStockAfter(plan, stockAfter.Value)
                : plan;

            // MODIFICADO: Agregar source_order_item_id
            await connection.ExecuteAsync(@"
                INSERT INTO inventory.movements (
                    company_id, branch_id, product_id, movement_type,
                    quantity, unit_cost, reference_type, reference_id,
                    notes, stock_after, source_order_item_id, created_by, created_at
                ) VALUES (
                    @CompanyId, @BranchId, @ProductId, @MovementType,
                    @Quantity, @UnitCost, @ReferenceType, @ReferenceId,
                    @Notes, @StockAfter, @SourceOrderItemId, @UserId, NOW()
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
                    completedPlan.SourceOrderItemId,  // ← NUEVO
                    context.UserId
                }, transaction);
        }
    }

    private static async Task LockAndValidateAvailabilityAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        InventoryTransactionContext context,
        IReadOnlyDictionary<long, decimal> requirements)
    {
        var productIds = requirements.Keys.OrderBy(id => id).ToArray();
        var locked = (await connection.QueryAsync<StockRow>(@"
            SELECT product_id AS ProductId, quantity AS Quantity
            FROM inventory.stock
            WHERE company_id = @CompanyId
              AND branch_id = @BranchId
              AND product_id = ANY(@ProductIds)
            ORDER BY product_id
            FOR UPDATE",
            new { context.CompanyId, context.BranchId, ProductIds = productIds }, transaction))
            .ToDictionary(row => row.ProductId);

        var committed = (await connection.QueryAsync<CommittedRow>($@"
            WITH {CommittedInventorySql.Cte}
            SELECT product_id AS ProductId,
                   committed_quantity AS Quantity
            FROM committed
            WHERE product_id = ANY(@ProductIds)",
            new
            {
                context.CompanyId,
                context.BranchId,
                ExcludedOrderId = context.ExcludedCommittedOrderId ?? -1L,
                ProductIds = productIds
            }, transaction)).ToDictionary(row => row.ProductId, row => row.Quantity);

        foreach (var requirement in requirements.OrderBy(entry => entry.Key))
        {
            if (!locked.TryGetValue(requirement.Key, out var stock))
                throw new BusinessException($"Stock insuficiente para el producto {requirement.Key}");

            var committedQuantity = committed.GetValueOrDefault(requirement.Key);
            if (stock.Quantity - committedQuantity < requirement.Value)
                throw new BusinessException($"Stock insuficiente para el producto {requirement.Key}");
        }
    }

    private sealed class StockRow
    {
        public long ProductId { get; init; }
        public decimal Quantity { get; init; }
    }

    private sealed class CommittedRow
    {
        public long ProductId { get; init; }
        public decimal Quantity { get; init; }
    }
}
```

---

### 2.4 Infrastructure Layer: `CheckoutRepository.cs`

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/CheckoutRepository.cs`

```csharp
// MODIFICADO: Pasar OrderItemId en SaleInventoryLine

private Task<IReadOnlyList<InventoryMovementPlan>> BuildInventoryMovementsAsync(
    IDbConnection connection,
    IDbTransaction transaction,
    CheckoutCommand command,
    CheckoutOrderRow order,
    CheckoutTableRow table,
    List<CheckoutItemRow> items)
{
    var lines = items.Select(item => new SaleInventoryLine(
        item.ProductId,
        item.ProductName,
        item.ProductType,
        item.Quantity,
        item.CostPrice,
        item.TrackStock,
        item.ProductExists,
        item.IsActive,
        item.IsForSale,
        item.Id)).ToList();  // ← NUEVO: pasar item.Id como OrderItemId

    return _inventoryPlanBuilder.BuildAsync(
        connection,
        transaction,
        new SaleInventoryPlanContext(
            command.CompanyId,
            ReferenceType: "order",
            ReferenceId: order.Id,
            SaleNotes: $"Venta Mesa {table.TableNumber} - {order.OrderNumber}",
            RecipeNotes: $"Consumo receta - Venta Mesa {table.TableNumber} - {order.OrderNumber}"),
        lines);
}
```

---

### 2.5 Infrastructure Layer: `RefundRepository.cs`

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/RefundRepository.cs`

```csharp
// NUEVO: Agregar clase OriginalMovementRow después de la línea 11

private sealed class OriginalMovementRow
{
    public long ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public bool TrackStock { get; init; }
}

// MODIFICADO: RestoreInventoryAsync (líneas 670-711)

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
            // NUEVO: Consultar movimientos con source_order_item_id
            var originalMovements = (await connection.QueryAsync<OriginalMovementRow>(@"
                SELECT m.product_id AS ProductId,
                       ABS(m.quantity) AS Quantity,
                       m.unit_cost AS UnitCost,
                       p.track_stock AS TrackStock
                FROM inventory.movements m
                JOIN inventory.products p ON p.id = m.product_id
                WHERE m.company_id = @CompanyId
                  AND m.branch_id = @BranchId
                  AND m.source_order_item_id = @OrderItemId
                  AND m.movement_type = 'recipe_consumption'
                  AND m.quantity < 0
                ORDER BY m.product_id", new
            {
                command.CompanyId,
                command.BranchId,
                OrderItemId = refundItem.OrderItemId
            }, transaction)).ToList();

            if (originalMovements.Count == 0)
            {
                // Fallback para ventas legacy
                var preparedItemsCount = orderItems.Count(i => i.ProductType == "prepared");
                
                if (preparedItemsCount > 1)
                {
                    throw new BusinessException(
                        "No se puede procesar el refund de esta orden legacy con múltiples productos preparados. " +
                        "Contacte al administrador para procesamiento manual.",
                        "legacy_multi_prepared_refund_not_supported");
                }
                
                _logger.LogWarning(
                    "Venta legacy sin source_order_item_id. Order={OrderId}, Item={OrderItemId}. " +
                    "Usando receta actual como fallback.",
                    order.Id, refundItem.OrderItemId);
                
                originalMovements = await GetCurrentRecipeAsFallback(
                    connection, transaction, command, orderItem.ProductId);
            }

            // Calcular proporción para refund parcial
            var refundRatio = refundItem.Quantity / orderItem.Quantity;

            foreach (var movement in originalMovements.Where(m => m.TrackStock))
            {
                await RestoreTrackedProductAsync(
                    connection, transaction, command,
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
            // Productos simples: sin cambios
            await RestoreTrackedProductAsync(
                connection, transaction, command,
                orderItem.ProductId,
                refundItem.Quantity,
                refundItem.UnitPrice,
                "refund",
                refundId,
                order.OrderNumber);
        }
    }
}

// NUEVO: Agregar método GetCurrentRecipeAsFallback

private static async Task<List<OriginalMovementRow>> GetCurrentRecipeAsFallback(
    System.Data.IDbConnection connection,
    System.Data.IDbTransaction transaction,
    RefundProcessCommand command,
    long productId)
{
    var currentRecipe = await connection.QueryAsync<OriginalMovementRow>(@"
        SELECT r.ingredient_id AS ProductId,
               r.quantity AS Quantity,
               p.cost_price AS UnitCost,
               p.track_stock AS TrackStock
        FROM inventory.recipes r
        JOIN inventory.products p
          ON p.id = r.ingredient_id AND p.company_id = r.company_id
        WHERE r.company_id = @CompanyId AND r.product_id = @ProductId", new
    {
        command.CompanyId,
        ProductId = productId
    }, transaction);

    return currentRecipe.ToList();
}
```

---

## PARTE 3: TESTS

### 3.1 Test: Refund después de cambio de receta

**Archivo**: `backend-dotnet/tests/Walos.Tests/Integration/RefundAtomicityIntegrationTests.cs`

```csharp
[SkippableFact]
public async Task Refund_After_Recipe_Change_Restores_Original_Ingredients_Not_Current()
{
    // Arrange: Crear producto preparado con receta V1
    var ctx = await SeedPreparedOrderAsync("Recipe change test");
    
    // Obtener movimientos originales
    var originalMovements = await GetMovementsByOrderAsync(ctx.Order);
    var originalCarne = originalMovements.Single(m => m.ProductId == ctx.IngredientId);
    
    // Cambiar receta (simular cambio en inventory.recipes)
    await ExecuteSqlAsync($@"
        UPDATE inventory.recipes
        SET quantity = 200
        WHERE product_id = {ctx.PreparedProductId}
          AND ingredient_id = {ctx.IngredientId}");
    
    // Act: Refund completo
    var result = await RefundAsync(ctx, "full", NewKey(), null);
    
    // Assert: Debe restituir cantidad original (150g), no la nueva (200g)
    var refundMovements = await GetRefundMovementsByOrderAsync(ctx.Order);
    var refundedCarne = refundMovements.Single(m => m.ProductId == ctx.IngredientId);
    
    Assert.Equal(originalCarne.Quantity, Math.Abs(refundedCarne.Quantity));
    Assert.NotEqual(200m, Math.Abs(refundedCarne.Quantity));
}
```

### 3.2 Test: Múltiples preparados con ingredientes compartidos

```csharp
[SkippableFact]
public async Task Refund_One_Item_From_Order_With_Multiple_Prepared_Products_Sharing_Ingredients()
{
    // Arrange: Crear dos productos preparados que comparten carne
    var hamburguesa = await SeedPreparedProductAsync("Hamburguesa", new[]
    {
        (IngredientId: 10, Quantity: 150m),  // Carne
        (IngredientId: 20, Quantity: 1m)     // Pan
    });
    
    var pizza = await SeedPreparedProductAsync("Pizza", new[]
    {
        (IngredientId: 10, Quantity: 200m),  // Carne (compartido)
        (IngredientId: 30, Quantity: 50m)    // Queso
    });
    
    // Vender ambos en la misma orden
    var order = await SeedOrderWithMultiplePreparedAsync(new[]
    {
        (ProductId: hamburguesa.ProductId, Quantity: 2m),
        (ProductId: pizza.ProductId, Quantity: 1m)
    });
    
    // Verificar movimientos agregados
    var movements = await GetMovementsByOrderAsync(order.Id);
    var carneMovement = movements.Single(m => m.ProductId == 10);
    Assert.Equal(-500m, carneMovement.Quantity);  // 2×150 + 1×200
    
    // Act: Refund solo de la pizza
    var pizzaItem = await GetOrderItemByProductAsync(order.Id, pizza.ProductId);
    await RefundAsync(order.Id, "partial", NewKey(), new[]
    {
        (OrderItemId: pizzaItem.Id, Quantity: 1m)
    });
    
    // Assert: Debe restituir solo 200g de carne (de la pizza), no 500g
    var refundMovements = await GetRefundMovementsByOrderAsync(order.Id);
    var refundedCarne = refundMovements.Single(m => m.ProductId == 10);
    
    Assert.Equal(200m, refundedCarne.Quantity);  // Solo la pizza
    Assert.Contains(refundMovements, m => m.ProductId == 30 && m.Quantity == 50m);  // Queso
    Assert.DoesNotContain(refundMovements, m => m.ProductId == 20);  // Pan NO (es de hamburguesa)
}
```

### 3.3 Test: Venta legacy con múltiples preparados

```csharp
[SkippableFact]
public async Task Refund_Legacy_Order_With_Multiple_Prepared_Products_Throws_Exception()
{
    // Arrange: Simular venta legacy eliminando source_order_item_id
    var order = await SeedOrderWithMultiplePreparedAsync(new[]
    {
        (ProductId: 100, Quantity: 2m),
        (ProductId: 101, Quantity: 1m)
    });
    
    await ExecuteSqlAsync($@"
        UPDATE inventory.movements
        SET source_order_item_id = NULL
        WHERE reference_type = 'order'
          AND reference_id = {order.Id}");
    
    // Act & Assert: Debe lanzar excepción
    var exception = await Assert.ThrowsAsync<BusinessException>(() =>
        RefundAsync(order.Id, "full", NewKey(), null));
    
    Assert.Equal("legacy_multi_prepared_refund_not_supported", exception.Code);
    Assert.Contains("múltiples productos preparados", exception.Message);
}
```

---

## PARTE 4: VALIDACIÓN Y ROLLOUT

### 4.1 Queries de validación

```sql
-- 1. Verificar que nuevos movimientos tienen source_order_item_id
SELECT COUNT(*) AS nuevos_movimientos_sin_source
FROM inventory.movements
WHERE created_at >= '2025-01-15'  -- Fecha de deploy
  AND movement_type IN ('sale', 'recipe_consumption')
  AND source_order_item_id IS NULL;
-- Resultado esperado: 0 (o solo movimientos de ajustes/transferencias)

-- 2. Verificar correlación correcta
SELECT 
    oi.id AS order_item_id,
    oi.product_id,
    oi.quantity AS sold_quantity,
    COUNT(m.id) AS movement_count,
    SUM(ABS(m.quantity)) AS total_movement_quantity
FROM sales.order_items oi
JOIN sales.orders o ON o.id = oi.order_id
JOIN inventory.products p ON p.id = oi.product_id
LEFT JOIN inventory.movements m 
    ON m.source_order_item_id = oi.id
   AND m.movement_type = 'recipe_consumption'
WHERE o.created_at >= '2025-01-15'
  AND p.product_type = 'prepared'
GROUP BY oi.id, oi.product_id, oi.quantity
HAVING COUNT(m.id) = 0;
-- Resultado esperado: 0 filas (todos los items tienen movimientos)

-- 3. Auditar refunds post-deploy
SELECT 
    r.id AS refund_id,
    r.order_id,
    ri.order_item_id,
    COUNT(m.id) AS movements_restored
FROM sales.refunds r
JOIN sales.refund_items ri ON ri.refund_id = r.id
LEFT JOIN inventory.movements m 
    ON m.reference_type = 'refund'
   AND m.reference_id = r.id
   AND m.movement_type = 'refund_recipe'
WHERE r.created_at >= '2025-01-15'
GROUP BY r.id, r.order_id, ri.order_item_id
HAVING COUNT(m.id) = 0;
-- Resultado esperado: 0 filas (todos los refunds generaron movimientos)
```

### 4.2 Plan de rollout

1. **Pre-deploy** (1 hora)
   - Backup de base de datos
   - Ejecutar migración en staging
   - Validar schema con queries de verificación

2. **Deploy** (1 hora)
   - Ejecutar migración 019 en producción
   - Deploy de código
   - Verificar que aplicación inicia

3. **Post-deploy** (1 semana)
   - Monitorear logs de fallback legacy
   - Ejecutar queries de validación diariamente
   - Revisar refunds procesados

---

## PARTE 5: ESTIMACIÓN FINAL

| Fase | Tiempo |
|------|--------|
| Desarrollo | 1-2 días |
| Tests | 1 día |
| Migración | 1 hora |
| QA en staging | 1 día |
| Deploy | 1 hora |
| **Total** | **3-4 días** |

---

## CONCLUSIÓN

Esta solución:
- ✅ Resuelve el caso general (múltiples preparados con ingredientes compartidos)
- ✅ Mantiene el ledger como fuente única de verdad
- ✅ No duplica información
- ✅ Permite auditoría completa
- ✅ Bloquea refunds legacy problemáticos en lugar de producir stock incorrecto

**Recomendación**: Implementar esta solución antes de permitir refunds de productos preparados en producción.

---

**Auditor**: Arquitectónico independiente  
**Fecha**: 2025-01-15  
**Versión**: 2.0 (Revisión crítica)
