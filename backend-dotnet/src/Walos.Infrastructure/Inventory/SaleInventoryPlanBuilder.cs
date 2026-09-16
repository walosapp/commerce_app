using System.Data;
using Dapper;
using Walos.Domain.Exceptions;
using Walos.Domain.Policies;

namespace Walos.Infrastructure.Inventory;

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
    long? OrderItemId = null);

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

        var plans = lines
            .Where(line => !IsPrepared(line.ProductType))
            .GroupBy(line => new { line.ProductId, line.TrackStock, line.UnitCost, line.OrderItemId })
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
                sourceOrderItemId: group.Key.OrderItemId))
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

        var preparedLines = lines.Where(line => IsPrepared(line.ProductType)).ToList();
        if (preparedLines.Any(line => !line.OrderItemId.HasValue))
            throw new BusinessException("La venta preparada no tiene trazabilidad por item de orden");

        plans.AddRange(preparedLines
            .SelectMany(line => recipes
                .Where(recipe => recipe.ProductId == line.ProductId)
                .Select(recipe => new
                {
                    line.OrderItemId,
                    recipe.IngredientId,
                    recipe.TrackStock,
                    recipe.UnitCost,
                    Quantity = recipe.Quantity * line.Quantity
                }))
            .GroupBy(requirement => new
            {
                requirement.OrderItemId,
                requirement.IngredientId,
                requirement.TrackStock,
                requirement.UnitCost
            })
            .Select(group => InventoryMovementPlanner.Create(
                InventoryMovementDirection.Outbound,
                group.Key.IngredientId,
                movementType: "recipe_consumption",
                quantity: group.Sum(requirement => requirement.Quantity),
                unitCost: group.Key.UnitCost,
                referenceType: context.ReferenceType,
                referenceId: context.ReferenceId,
                requiresStock: group.Key.TrackStock,
                notes: context.RecipeNotes,
                sourceOrderItemId: group.Key.OrderItemId)));

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
