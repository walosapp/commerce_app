using Walos.Domain.Exceptions;
using Walos.Domain.Policies;

namespace Walos.Tests.Policies;

public class SaleItemPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_Must_Be_Positive(double quantity)
    {
        Assert.Throws<ValidationException>(() => SaleItemPolicy.NormalizeQuantity((decimal)quantity));
    }

    [Fact]
    public void Two_Decimal_Quantity_Is_Currently_Supported()
    {
        Assert.Equal(1.25m, SaleItemPolicy.NormalizeQuantity(1.25m));
    }

    [Fact]
    public void Three_Decimal_Quantity_Is_Temporarily_Rejected_Until_Order_Items_Migrate()
    {
        Assert.Throws<ValidationException>(() => SaleItemPolicy.NormalizeQuantity(1.255m));
        Assert.Equal(3, SaleItemPolicy.TargetQuantityDecimals);
        Assert.Equal(2, SaleItemPolicy.CurrentQuantityDecimals);
    }

    [Fact]
    public void Snapshot_Normalizes_Server_Values_And_Subtotal()
    {
        var snapshot = SaleItemPolicy.CreateSnapshot(7, "Producto real", 1.25m, 10.005m);

        Assert.Equal(7, snapshot.ProductId);
        Assert.Equal("Producto real", snapshot.ProductName);
        Assert.Equal(1.25m, snapshot.Quantity);
        Assert.Equal(10.01m, snapshot.UnitPrice);
        Assert.Equal(12.51m, snapshot.Subtotal);
    }

    [Fact]
    public void Prepared_Product_Requires_At_Least_One_Valid_Ingredient()
    {
        Assert.Throws<ValidationException>(() => SaleItemPolicy.ValidatePreparedRecipe("prepared", 0));
        SaleItemPolicy.ValidatePreparedRecipe("prepared", 1);
        SaleItemPolicy.ValidatePreparedRecipe("simple", 0);
    }
}
