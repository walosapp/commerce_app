using Walos.Application.DTOs.Delivery;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IDeliveryService
{
    Task<IEnumerable<DeliveryOrder>> GetOrdersAsync(long companyId, long branchId, string? status, DateTime? dateFrom, DateTime? dateTo);
    Task<DeliveryOrder?> GetOrderByIdAsync(long orderId, long companyId);
    Task<DeliveryOrder> CreateOrderAsync(long companyId, long branchId, long userId, CreateDeliveryOrderRequest request);
    Task AcceptOrderAsync(long orderId, long companyId, long userId, string? comment);
    Task RejectOrderAsync(long orderId, long companyId, long userId, string comment);
    Task PrepareOrderAsync(long orderId, long companyId, long userId, string? comment);
    Task ReadyOrderAsync(long orderId, long companyId, long userId, string? comment);
    Task DispatchOrderAsync(long orderId, long companyId, long userId, string? comment);
    Task DeliverOrderAsync(long orderId, long companyId, long userId, string? comment);
    Task CancelOrderAsync(long orderId, long companyId, long userId, string comment);
    Task ReturnOrderAsync(long orderId, long companyId, long userId, string comment);
}
