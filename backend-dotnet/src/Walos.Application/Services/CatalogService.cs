using Walos.Application.DTOs.Inventory;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;

namespace Walos.Application.Services;

public class CatalogService : ICatalogService
{
    private readonly ICatalogRepository _repository;

    public CatalogService(ICatalogRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<CategoryResponse>> GetCategoriesAsync(long companyId)
        => _repository.GetCategoriesAsync(companyId);

    public async Task<CategoryResponse> CreateCategoryAsync(long companyId, SaveCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre es requerido");

        return await _repository.CreateCategoryAsync(companyId, request);
    }

    public async Task<CategoryResponse?> UpdateCategoryAsync(long id, long companyId, SaveCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre es requerido");

        return await _repository.UpdateCategoryAsync(id, companyId, request);
    }

    public Task<bool> SetCategoryStatusAsync(long id, long companyId, bool isActive)
        => _repository.SetCategoryActiveAsync(id, companyId, isActive);

    public Task<bool> DeleteCategoryAsync(long id, long companyId)
        => _repository.DeleteCategoryAsync(id, companyId);

    public Task<IEnumerable<UnitResponse>> GetUnitsAsync(long companyId)
        => _repository.GetUnitsAsync(companyId);

    public async Task<UnitResponse> CreateUnitAsync(long companyId, SaveUnitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Abbreviation))
            throw new ValidationException("Nombre y abreviatura son requeridos");

        return await _repository.CreateUnitAsync(companyId, request);
    }

    public async Task<UnitResponse?> UpdateUnitAsync(long id, long companyId, SaveUnitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre es requerido");

        return await _repository.UpdateUnitAsync(id, companyId, request);
    }

    public Task<bool> SetUnitStatusAsync(long id, long companyId, bool isActive)
        => _repository.SetUnitActiveAsync(id, companyId, isActive);

    public Task<bool> DeleteUnitAsync(long id, long companyId)
        => _repository.DeleteUnitAsync(id, companyId);
}

