using Walos.Application.DTOs.Suppliers;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface ISuppliersService
{
    Task<IEnumerable<Supplier>> GetAllAsync(long companyId, long? branchId);
    Task<Supplier?> GetByIdAsync(long supplierId, long companyId, long? branchId);
    Task<Supplier> CreateAsync(long companyId, long? branchId, long userId, CreateSupplierRequest request);
    Task<Supplier?> UpdateAsync(long companyId, long? branchId, long supplierId, UpdateSupplierRequest request);
    Task<bool> DeleteAsync(long supplierId, long companyId, long? branchId);
    Task<SupplierProduct> AddProductAsync(long companyId, long? branchId, long supplierId, AddSupplierProductRequest request);
    Task<bool> RemoveProductAsync(long companyId, long? branchId, long supplierId, long productId);
    Task<SuggestedOrderResponse?> GetSuggestedOrderAsync(long supplierId, long companyId, long branchId);
}
