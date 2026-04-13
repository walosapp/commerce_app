using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IFinanceRepository
{
    Task<IEnumerable<FinancialCategory>> GetCategoriesAsync(long companyId, string? type = null);
    Task<FinancialCategory?> GetCategoryByIdAsync(long id, long companyId);
    Task<FinancialCategory> CreateCategoryAsync(FinancialCategory category);
    Task<FinancialCategory> UpdateCategoryAsync(FinancialCategory category);
    Task SoftDeleteCategoryAsync(long id, long companyId);

    Task<int> InitMonthFromFinancialItemsAsync(long companyId, long? branchId, DateTime monthStart, long? userId, IReadOnlyCollection<FinanceMonthSelectionItem>? selectedItems = null);

    Task<IEnumerable<FinancialEntry>> GetEntriesAsync(long companyId, long? branchId = null, string? type = null, long? categoryId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<FinancialEntry?> GetEntryByIdAsync(long id, long companyId);
    Task<FinancialEntry> CreateEntryAsync(FinancialEntry entry);
    Task<FinancialEntry> UpdateEntryAsync(FinancialEntry entry);
    Task SoftDeleteEntryAsync(long id, long companyId);
    Task<FinancialSummary> GetSummaryAsync(long companyId, long? branchId = null, DateTime? startDate = null, DateTime? endDate = null);
}
