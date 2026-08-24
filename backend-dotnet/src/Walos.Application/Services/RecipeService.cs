using Walos.Application.DTOs.Inventory;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;

namespace Walos.Application.Services;

public class RecipeService : IRecipeService
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public RecipeService(IRecipeRepository recipeRepository, IInventoryRepository inventoryRepository)
    {
        _recipeRepository = recipeRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<IEnumerable<Recipe>> GetByProductAsync(long productId, long companyId)
    {
        await GetActiveProductAsync(productId, companyId, "Producto");
        return await _recipeRepository.GetByProductAsync(productId, companyId);
    }

    public async Task<Recipe> UpsertIngredientAsync(long productId, long companyId, UpsertRecipeIngredientRequest request)
    {
        if (request.IngredientId <= 0)
            throw new ValidationException("IngredientId requerido");
        if (request.Quantity <= 0)
            throw new ValidationException("La cantidad debe ser mayor a 0");

        await GetActiveProductAsync(productId, companyId, "Producto");
        await GetActiveProductAsync(request.IngredientId, companyId, "Ingrediente");
        if (request.UnitId.HasValue &&
            !await _inventoryRepository.IsActiveUnitInCompanyAsync(request.UnitId.Value, companyId))
            throw new NotFoundException("Unidad");

        var recipe = new Recipe
        {
            CompanyId = companyId,
            ProductId = productId,
            IngredientId = request.IngredientId,
            Quantity = request.Quantity,
            UnitId = request.UnitId,
            Notes = request.Notes,
        };

        var result = await _recipeRepository.UpsertIngredientAsync(recipe);
        await RecalculatePreparedProductPricingAsync(productId, companyId);
        return result;
    }

    public async Task<bool> RemoveIngredientAsync(long productId, long ingredientId, long companyId)
    {
        await GetActiveProductAsync(productId, companyId, "Producto");
        await GetActiveProductAsync(ingredientId, companyId, "Ingrediente");
        var ok = await _recipeRepository.RemoveIngredientAsync(productId, ingredientId, companyId);
        if (ok)
            await RecalculatePreparedProductPricingAsync(productId, companyId);
        return ok;
    }

    public async Task ClearRecipeAsync(long productId, long companyId)
    {
        await GetActiveProductAsync(productId, companyId, "Producto");
        await _recipeRepository.ClearRecipeAsync(productId, companyId);
        await RecalculatePreparedProductPricingAsync(productId, companyId);
    }

    private async Task RecalculatePreparedProductPricingAsync(long productId, long companyId)
    {
        var product = await _inventoryRepository.GetProductByIdAsync(productId, companyId);
        if (product is null || product.ProductType != "prepared")
            return;

        var ingredients = (await _recipeRepository.GetByProductAsync(productId, companyId)).ToList();

        decimal calculatedCost = 0m;
        foreach (var ingredient in ingredients)
        {
            var ingredientProduct = await _inventoryRepository.GetProductByIdAsync(ingredient.IngredientId, companyId);
            var ingredientCost = ingredientProduct?.CostPrice ?? 0m;
            calculatedCost += ingredient.Quantity * ingredientCost;
        }

        decimal? calculatedSalePrice = null;
        if (product.MarginPercentage.HasValue)
        {
            calculatedSalePrice = Math.Round(
                calculatedCost * (1 + (product.MarginPercentage.Value / 100m)),
                2,
                MidpointRounding.AwayFromZero
            );
        }

        await _inventoryRepository.UpdateProductCostAndPriceAsync(
            productId,
            companyId,
            Math.Round(calculatedCost, 2, MidpointRounding.AwayFromZero),
            calculatedSalePrice
        );
    }

    private async Task<Product> GetActiveProductAsync(long productId, long companyId, string resourceName)
    {
        var product = await _inventoryRepository.GetProductByIdAsync(productId, companyId);
        if (product is null || !product.IsActive)
            throw new NotFoundException(resourceName);
        return product;
    }
}

