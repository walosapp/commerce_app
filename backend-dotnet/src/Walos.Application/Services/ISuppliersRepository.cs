using Walos.Application.Services;
using Walos.Application.DTOs.Suppliers;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface ISuppliersRepository
{
    Task<IEnumerable<Supplier>> GetAllAsync(long companyId, long? branchId);
    Task<Supplier?> GetByIdAsync(long supplierId, long companyId, long? branchId);
    Task<Supplier?> CreateAsync(Supplier supplier);
    Task<Supplier?> UpdateAsync(Supplier supplier, long? branchId);
    Task<bool> SoftDeleteAsync(long supplierId, long companyId, long? branchId);
    Task<IEnumerable<SupplierProduct>> GetSupplierProductsAsync(long supplierId, long companyId);
    Task<SupplierProduct?> AddSupplierProductAsync(long companyId, long? branchId, SupplierProduct supplierProduct);
    Task<bool> RemoveSupplierProductAsync(long companyId, long? branchId, long supplierId, long productId);
    Task<IEnumerable<Supplier>> GetSuppliersForProductAsync(long productId, long companyId);
    Task<IEnumerable<SuggestedOrderItem>> GetLowStockItemsForSupplierAsync(long supplierId, long companyId, long branchId);
}


