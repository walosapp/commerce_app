using Moq;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class RecipeServiceTests
{
    private readonly Mock<IRecipeRepository> _recipe = new();
    private readonly Mock<IInventoryRepository> _inventory = new();
    private readonly RecipeService _service;

    public RecipeServiceTests()
    {
        _service = new RecipeService(_recipe.Object, _inventory.Object);
    }

    [Fact]
    public async Task Upsert_Rejects_Parent_Product_From_Another_Company()
    {
        _inventory.Setup(r => r.GetProductByIdAsync(10, 1)).ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpsertIngredientAsync(
            10, 1, Request(20, 30)));

        _recipe.Verify(r => r.UpsertIngredientAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_Rejects_Ingredient_From_Another_Company()
    {
        _inventory.Setup(r => r.GetProductByIdAsync(10, 1)).ReturnsAsync(ActiveProduct(10));
        _inventory.Setup(r => r.GetProductByIdAsync(20, 1)).ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpsertIngredientAsync(
            10, 1, Request(20, 30)));

        _recipe.Verify(r => r.UpsertIngredientAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_Rejects_Unit_From_Another_Company()
    {
        _inventory.Setup(r => r.GetProductByIdAsync(10, 1)).ReturnsAsync(ActiveProduct(10));
        _inventory.Setup(r => r.GetProductByIdAsync(20, 1)).ReturnsAsync(ActiveProduct(20));
        _inventory.Setup(r => r.IsActiveUnitInCompanyAsync(30, 1)).ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpsertIngredientAsync(
            10, 1, Request(20, 30)));

        _recipe.Verify(r => r.UpsertIngredientAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_Rejects_Inactive_Ingredient()
    {
        _inventory.Setup(r => r.GetProductByIdAsync(10, 1)).ReturnsAsync(ActiveProduct(10));
        _inventory.Setup(r => r.GetProductByIdAsync(20, 1))
            .ReturnsAsync(new Product { Id = 20, CompanyId = 1, IsActive = false });

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpsertIngredientAsync(
            10, 1, Request(20, null)));

        _recipe.Verify(r => r.UpsertIngredientAsync(It.IsAny<Recipe>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_Accepts_Active_Tenant_Owned_Recipe_Data()
    {
        _inventory.Setup(r => r.GetProductByIdAsync(10, 1)).ReturnsAsync(ActiveProduct(10));
        _inventory.Setup(r => r.GetProductByIdAsync(20, 1)).ReturnsAsync(ActiveProduct(20));
        _inventory.Setup(r => r.IsActiveUnitInCompanyAsync(30, 1)).ReturnsAsync(true);
        _recipe.Setup(r => r.UpsertIngredientAsync(It.IsAny<Recipe>()))
            .ReturnsAsync((Recipe r) => r);
        _recipe.Setup(r => r.GetByProductAsync(10, 1)).ReturnsAsync([]);

        var result = await _service.UpsertIngredientAsync(10, 1, Request(20, 30));

        Assert.Equal(1, result.CompanyId);
        Assert.Equal(10, result.ProductId);
        Assert.Equal(20, result.IngredientId);
        _recipe.Verify(r => r.UpsertIngredientAsync(It.Is<Recipe>(x =>
            x.CompanyId == 1 && x.ProductId == 10 && x.IngredientId == 20 && x.UnitId == 30)), Times.Once);
    }

    private static UpsertRecipeIngredientRequest Request(long ingredientId, long? unitId) => new()
    {
        IngredientId = ingredientId,
        UnitId = unitId,
        Quantity = 2,
    };

    private static Product ActiveProduct(long id) => new()
    {
        Id = id,
        CompanyId = 1,
        IsActive = true,
        ProductType = "simple",
    };
}
