using Walos.Application.DTOs.Inventory;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IRecipeService
{
    Task<IEnumerable<Recipe>> GetByProductAsync(long productId, long companyId);
    Task<Recipe> UpsertIngredientAsync(long productId, long companyId, UpsertRecipeIngredientRequest request);
    Task<bool> RemoveIngredientAsync(long productId, long ingredientId, long companyId);
    Task ClearRecipeAsync(long productId, long companyId);
}
