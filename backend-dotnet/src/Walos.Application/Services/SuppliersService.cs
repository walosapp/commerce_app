using Walos.Application.DTOs.Suppliers;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;

namespace Walos.Application.Services;

public class SuppliersService : ISuppliersService
{
    private readonly ISuppliersRepository _repository;

    public SuppliersService(ISuppliersRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Supplier>> GetAllAsync(long companyId, long? branchId)
        => _repository.GetAllAsync(companyId, branchId);

    public Task<Supplier?> GetByIdAsync(long supplierId, long companyId)
        => _repository.GetByIdAsync(supplierId, companyId);

    public async Task<Supplier> CreateAsync(long companyId, long? branchId, long userId, CreateSupplierRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre es requerido");

        var supplier = new Supplier
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = request.Name,
            ContactName = request.ContactName,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            Notes = request.Notes,
            CreatedBy = userId,
        };

        return await _repository.CreateAsync(supplier);
    }

    public async Task<Supplier?> UpdateAsync(long companyId, long supplierId, UpdateSupplierRequest request)
    {
        var supplier = new Supplier
        {
            Id = supplierId,
            CompanyId = companyId,
            Name = request.Name,
            ContactName = request.ContactName,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            Notes = request.Notes,
        };

        return await _repository.UpdateAsync(supplier);
    }

    public Task<bool> DeleteAsync(long supplierId, long companyId)
        => _repository.SoftDeleteAsync(supplierId, companyId);

    public async Task<SupplierProduct> AddProductAsync(long supplierId, AddSupplierProductRequest request)
    {
        var supplierProduct = new SupplierProduct
        {
            SupplierId = supplierId,
            ProductId = request.ProductId,
            SupplierSku = request.SupplierSku,
            UnitCost = request.UnitCost,
            LeadTimeDays = request.LeadTimeDays,
            Notes = request.Notes,
        };

        return await _repository.AddSupplierProductAsync(supplierProduct);
    }

    public Task<bool> RemoveProductAsync(long supplierId, long productId)
        => _repository.RemoveSupplierProductAsync(supplierId, productId);

    public async Task<SuggestedOrderResponse?> GetSuggestedOrderAsync(long supplierId, long companyId, long branchId)
    {
        var supplier = await _repository.GetByIdAsync(supplierId, companyId);
        if (supplier is null)
            return null;

        var items = (await _repository.GetLowStockItemsForSupplierAsync(supplierId, companyId, branchId)).ToList();

        return new SuggestedOrderResponse
        {
            SupplierId = supplierId,
            SupplierName = supplier.Name,
            Items = items,
            TotalEstimatedCost = items.Sum(i => i.EstimatedCost ?? 0),
        };
    }
}

