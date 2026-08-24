using Walos.Application.DTOs.Suppliers;

namespace Walos.Application.Services;

public interface IPurchaseOrderService
{
    Task<IEnumerable<PurchaseOrderResponse>> GetAllAsync(long companyId, long? branchId, long? supplierId = null);
    Task<PurchaseOrderResponse?> GetByIdAsync(long id, long companyId, long? branchId);
    Task<PurchaseOrderResponse> CreateAsync(long companyId, long branchId, long userId, CreatePurchaseOrderRequest request);
    Task<PurchaseOrderResponse> ReceiveAsync(long id, long companyId, long branchId, long userId, ReceivePurchaseOrderRequest request);
    Task<bool> CancelAsync(long id, long companyId, long? branchId);
}
