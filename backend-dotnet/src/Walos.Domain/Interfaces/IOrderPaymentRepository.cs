using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IOrderPaymentRepository
{
    Task<OrderPayment> CreateAsync(OrderPayment payment);
    Task<IEnumerable<OrderPayment>> GetByOrderAsync(long orderId, long companyId);
    Task<IEnumerable<PaymentMethodSummary>> GetSummaryByCashRegisterAsync(long cashRegisterId, long companyId);
    Task<IEnumerable<PaymentMethodSummary>> GetSummaryByDateRangeAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo);
}
