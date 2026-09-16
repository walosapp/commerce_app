using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.Services;
using Walos.Application.Storage;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class SaleCatalogServiceTests
{
    [Fact]
    public async Task GetSaleCatalog_Matches_Checkout_Configuration_Rules()
    {
        var repository = new Mock<IInventoryRepository>();
        repository.Setup(r => r.IsActiveBranchInCompanyAsync(20, 10)).ReturnsAsync(true);
        repository.Setup(r => r.GetStockByBranchAsync(20, 10)).ReturnsAsync(
        [
            Stock(1, "Free simple", "simple", salePrice: 0, hasValidRecipe: true),
            Stock(2, "Prepared without recipe", "prepared", salePrice: 25, hasValidRecipe: false),
            Stock(3, "Free prepared with recipe", "prepared", salePrice: 0, hasValidRecipe: true),
            Stock(4, "Negative price", "simple", salePrice: -1, hasValidRecipe: true),
            Stock(5, "Not for sale", "simple", salePrice: 25, hasValidRecipe: true, isForSale: false)
        ]);
        var service = new InventoryService(
            repository.Object,
            Mock.Of<IAiService>(),
            Mock.Of<IFileStorage>(),
            Mock.Of<IAiCapabilityGuard>(),
            Mock.Of<ILogger<InventoryService>>());

        var result = await service.GetSaleCatalogAsync(10, 20, requestedBranchId: null);

        Assert.True(result.Single(item => item.ProductId == 1).IsConfiguredForSale);
        Assert.False(result.Single(item => item.ProductId == 2).IsConfiguredForSale);
        Assert.True(result.Single(item => item.ProductId == 3).IsConfiguredForSale);
        Assert.False(result.Single(item => item.ProductId == 4).IsConfiguredForSale);
        Assert.False(result.Single(item => item.ProductId == 5).IsConfiguredForSale);
    }

    private static Stock Stock(
        long productId,
        string name,
        string productType,
        decimal salePrice,
        bool hasValidRecipe,
        bool isForSale = true) => new()
        {
            ProductId = productId,
            ProductName = name,
            ProductType = productType,
            CostPrice = 0,
            SalePrice = salePrice,
            HasValidRecipe = hasValidRecipe,
            IsForSale = isForSale,
            TrackStock = false
        };
}
