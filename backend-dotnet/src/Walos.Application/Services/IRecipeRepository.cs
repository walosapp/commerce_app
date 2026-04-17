using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IRecipeRepository
{
    Task<IEnumerable<Recipe>> GetByProductAsync(long productId, long companyId);
    Task<Recipe> UpsertIngredientAsync(Recipe recipe);
    Task<bool> RemoveIngredientAsync(long productId, long ingredientId, long companyId);
    Task<bool> ClearRecipeAsync(long productId, long companyId);
    Task<IEnumerable<Recipe>> GetAllIngredientsForSaleAsync(IEnumerable<(long ProductId, decimal Qty)> soldItems, long companyId);
}
