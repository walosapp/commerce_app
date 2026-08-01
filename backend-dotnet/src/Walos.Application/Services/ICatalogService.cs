using Walos.Application.DTOs.Inventory;

namespace Walos.Application.Services;

public interface ICatalogService
{
    Task<IEnumerable<CategoryResponse>> GetCategoriesAsync(long companyId);
    Task<CategoryResponse> CreateCategoryAsync(long companyId, SaveCategoryRequest request);
    Task<CategoryResponse?> UpdateCategoryAsync(long id, long companyId, SaveCategoryRequest request);
    Task<bool> SetCategoryStatusAsync(long id, long companyId, bool isActive);
    Task<bool> DeleteCategoryAsync(long id, long companyId);
    Task<IEnumerable<UnitResponse>> GetUnitsAsync(long companyId);
    Task<UnitResponse> CreateUnitAsync(long companyId, SaveUnitRequest request);
    Task<UnitResponse?> UpdateUnitAsync(long id, long companyId, SaveUnitRequest request);
    Task<bool> SetUnitStatusAsync(long id, long companyId, bool isActive);
    Task<bool> DeleteUnitAsync(long id, long companyId);
}
