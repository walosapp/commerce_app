using Walos.Domain.Policies;

namespace Walos.Tests.Policies;

public class InventoryMovementPlannerTests
{
    [Fact]
    public void Outbound_Plan_Contains_Traceability_And_Real_Cost()
    {
        var plan = InventoryMovementPlanner.Create(
            InventoryMovementDirection.Outbound,
            productId: 15,
            movementType: "sale",
            quantity: 1.25m,
            unitCost: 7.50m,
            referenceType: "order",
            referenceId: 99,
            requiresStock: true,
            notes: "Venta");

        Assert.Equal(InventoryMovementDirection.Outbound, plan.Direction);
        Assert.Equal(15, plan.ProductId);
        Assert.Equal(1.25m, plan.Quantity);
        Assert.Equal(7.50m, plan.UnitCost);
        Assert.Equal("order", plan.ReferenceType);
        Assert.Equal(99, plan.ReferenceId);
        Assert.Null(plan.StockAfter);

        var completed = InventoryMovementPlanner.WithStockAfter(plan, 8.75m);
        Assert.Equal(8.75m, completed.StockAfter);
    }
}
