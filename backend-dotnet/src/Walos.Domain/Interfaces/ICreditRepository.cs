using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ICreditRepository
{
    Task<Credit> CreateCreditAsync(Credit credit);
    Task<IEnumerable<Credit>> GetCreditsAsync(long companyId, string? status, string? search);
    Task<Credit?> GetCreditByIdAsync(long creditId, long companyId);
    Task<CreditPayment> AddPaymentAsync(CreditPayment payment);
    Task UpdateCreditAfterPaymentAsync(long creditId, long companyId, decimal newAmountPaid, decimal newCreditAmount, string newStatus, DateTime? paidAt);
    Task CancelCreditAsync(long creditId, long companyId);
}
