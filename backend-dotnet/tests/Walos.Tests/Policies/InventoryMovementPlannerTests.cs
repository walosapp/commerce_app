using Walos.Domain.Policies;
using Walos.Domain.Exceptions;

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
            notes: "Venta",
            sourceOrderItemId: 501);

        Assert.Equal(InventoryMovementDirection.Outbound, plan.Direction);
        Assert.Equal(15, plan.ProductId);
        Assert.Equal(1.25m, plan.Quantity);
        Assert.Equal(7.50m, plan.UnitCost);
        Assert.Equal("order", plan.ReferenceType);
        Assert.Equal(99, plan.ReferenceId);
        Assert.Equal(501, plan.SourceOrderItemId);
        Assert.Null(plan.StockAfter);

        var completed = InventoryMovementPlanner.WithStockAfter(plan, 8.75m);
        Assert.Equal(8.75m, completed.StockAfter);
    }

    [Theory]
    [InlineData(1.2344, 1.234)]
    [InlineData(1.2345, 1.235)]
    public void Quantity_Uses_Inventory_Three_Decimal_Precision(double input, double expected)
    {
        var plan = InventoryMovementPlanner.Create(
            InventoryMovementDirection.Outbound,
            productId: 1,
            movementType: "sale",
            quantity: (decimal)input,
            unitCost: 1m,
            referenceType: "order",
            referenceId: 1,
            requiresStock: true);

        Assert.Equal((decimal)expected, plan.Quantity);
    }

    [Fact]
    public void Quantity_Below_Inventory_Precision_Is_Rejected()
    {
        Assert.Throws<ValidationException>(() => InventoryMovementPlanner.Create(
            InventoryMovementDirection.Outbound,
            productId: 1,
            movementType: "recipe_consumption",
            quantity: 0.0001m,
            unitCost: 1m,
            referenceType: "order",
            referenceId: 1,
            requiresStock: true));
    }

    [Theory]
    [InlineData(4, 2, 0.5, 1)]
    [InlineData(4, 2, 2, 4)]
    public void Historical_Refund_Uses_Original_Item_Consumption(
        double originalConsumption,
        double soldQuantity,
        double refundQuantity,
        double expected)
    {
        var restored = InventoryMovementPlanner.CalculateHistoricalRefundQuantity(
            (decimal)originalConsumption,
            (decimal)soldQuantity,
            (decimal)refundQuantity);

        Assert.Equal((decimal)expected, restored);
    }

    [Fact]
    public void Historical_Refund_Rejects_Quantity_Above_Original_Sale()
    {
        Assert.Throws<ValidationException>(() =>
            InventoryMovementPlanner.CalculateHistoricalRefundQuantity(4m, 2m, 2.5m));
    }
}
