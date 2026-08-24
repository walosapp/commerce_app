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

    public Task<Supplier?> GetByIdAsync(long supplierId, long companyId, long? branchId)
        => _repository.GetByIdAsync(supplierId, companyId, branchId);

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

        return await _repository.CreateAsync(supplier)
            ?? throw new NotFoundException("Sucursal");
    }

    public async Task<Supplier?> UpdateAsync(long companyId, long? branchId, long supplierId, UpdateSupplierRequest request)
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

        return await _repository.UpdateAsync(supplier, branchId);
    }

    public Task<bool> DeleteAsync(long supplierId, long companyId, long? branchId)
        => _repository.SoftDeleteAsync(supplierId, companyId, branchId);

    public async Task<SupplierProduct> AddProductAsync(long companyId, long? branchId, long supplierId, AddSupplierProductRequest request)
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

        return await _repository.AddSupplierProductAsync(companyId, branchId, supplierProduct)
            ?? throw new NotFoundException("Proveedor o producto");
    }

    public Task<bool> RemoveProductAsync(long companyId, long? branchId, long supplierId, long productId)
        => _repository.RemoveSupplierProductAsync(companyId, branchId, supplierId, productId);

    public async Task<SuggestedOrderResponse?> GetSuggestedOrderAsync(long supplierId, long companyId, long branchId)
    {
        var supplier = await _repository.GetByIdAsync(supplierId, companyId, branchId);
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

