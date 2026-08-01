using Walos.Application.DTOs.Suppliers;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface ISuppliersService
{
    Task<IEnumerable<Supplier>> GetAllAsync(long companyId, long? branchId);
    Task<Supplier?> GetByIdAsync(long supplierId, long companyId);
    Task<Supplier> CreateAsync(long companyId, long? branchId, long userId, CreateSupplierRequest request);
    Task<Supplier?> UpdateAsync(long companyId, long supplierId, UpdateSupplierRequest request);
    Task<bool> DeleteAsync(long supplierId, long companyId);
    Task<SupplierProduct> AddProductAsync(long supplierId, AddSupplierProductRequest request);
    Task<bool> RemoveProductAsync(long supplierId, long productId);
    Task<SuggestedOrderResponse?> GetSuggestedOrderAsync(long supplierId, long companyId, long branchId);
}
