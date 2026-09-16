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

            await connection.ExecuteAsync(@"
                INSERT INTO inventory.movements (
                    company_id, branch_id, product_id, movement_type,
                    quantity, unit_cost, reference_type, reference_id,
                    source_order_item_id, notes, stock_after, created_by, created_at
                ) VALUES (
                    @CompanyId, @BranchId, @ProductId, @MovementType,
                    @Quantity, @UnitCost, @ReferenceType, @ReferenceId,
                    @SourceOrderItemId, @Notes, @StockAfter, @UserId, NOW()
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
                    completedPlan.SourceOrderItemId,
                    completedPlan.Notes,
                    completedPlan.StockAfter,
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
