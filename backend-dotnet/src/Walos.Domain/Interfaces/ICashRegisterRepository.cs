using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ICashRegisterRepository
{
    Task<CashRegister> OpenAsync(CashRegister register);
    Task<CashRegister?> GetActiveByUserAsync(long companyId, long branchId, long userId);
    Task<CashRegister?> GetByIdAsync(long id, long companyId);
    Task<CashRegister?> GetByIdAsync(long id, long companyId, long branchId);
    Task<CashRegister?> CloseAsync(long id, long companyId, long branchId, long closedBy, decimal closingAmount, string? notes);
    Task<CashMovement?> AddMovementAsync(CashMovement movement, long branchId);
    Task<IEnumerable<CashMovement>> GetMovementsAsync(long cashRegisterId, long companyId, long branchId);
    Task<decimal> GetCompletedRefundTotalAsync(long cashRegisterId, long companyId, long branchId);
    Task UpdateTotalsAsync(long id, long companyId, decimal totalSales, decimal totalCashSales, decimal totalCardSales, decimal totalTransferSales, decimal totalOtherSales, decimal totalDiscounts, decimal totalCredits, decimal totalTips, int orderCount);
    Task<IEnumerable<CashRegister>> GetHistoryAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit);
    Task<int> GetHistoryCountAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo);
}
