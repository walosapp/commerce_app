using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ICreditRepository
{
    Task<Credit> CreateCreditAsync(Credit credit);
    Task<IEnumerable<Credit>> GetCreditsAsync(long companyId, long branchId, string? status, string? search);
    Task<Credit?> GetCreditByIdAsync(long creditId, long companyId, long branchId);
    Task<CreditPayment> AddPaymentAsync(CreditPayment payment);
    Task<Credit> ProcessPaymentAsync(CreditPaymentCommand command);
    Task UpdateCreditAfterPaymentAsync(long creditId, long companyId, decimal newAmountPaid, decimal newCreditAmount, string newStatus, DateTime? paidAt);
    Task<bool> CancelCreditAsync(long creditId, long companyId, long branchId);
}
