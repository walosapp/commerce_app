using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IRefundRepository
{
    Task<Refund> CreateAsync(Refund refund);
    Task CreateItemsAsync(long refundId, List<RefundItem> items);
    Task<Refund?> GetByIdAsync(long id, long companyId, long branchId);
    Task<IEnumerable<Refund>> GetByOrderIdAsync(long orderId, long companyId, long branchId);
    Task<IEnumerable<Refund>> GetAllAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit);
    Task<int> GetCountAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo);
    Task UpdateOrderRefundStatusAsync(long orderId, long companyId, string refundStatus);
    Task<RefundProcessResult> ProcessAsync(RefundProcessCommand command);
}
