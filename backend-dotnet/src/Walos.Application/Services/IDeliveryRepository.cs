using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IDeliveryRepository
{
    Task<IEnumerable<DeliveryOrder>> GetOrdersAsync(long companyId, long branchId, string? status, DateTime? dateFrom, DateTime? dateTo);
    Task<DeliveryOrder?> GetOrderByIdAsync(long orderId, long companyId, long branchId);
    Task<DeliveryOrder> CreateOrderAsync(DeliveryOrder order, List<DeliveryOrderItem> items);
    Task<bool> UpdateOrderStatusAsync(long orderId, long companyId, long branchId, string expectedStatus, string newStatus, string? comment, long? changedBy, Dictionary<string, DateTime?> timestamps);
    Task<IEnumerable<DeliveryStatusHistory>> GetStatusHistoryAsync(long orderId, long companyId, long branchId);
}


