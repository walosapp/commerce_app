using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IFinanceRepository
{
    Task<bool> IsActiveBranchInCompanyAsync(long branchId, long companyId);
    Task<IEnumerable<FinancialCategory>> GetCategoriesAsync(long companyId, string? type = null);
    Task<IEnumerable<FinancialCategory>> GetCategoriesAsync(long companyId, string? type, long? branchId);
    Task<FinancialCategory?> GetCategoryByIdAsync(long id, long companyId);
    Task<FinancialCategory?> GetCategoryByIdAsync(long id, long companyId, long? branchId, bool includeGlobal);
    Task<FinancialCategory> CreateCategoryAsync(FinancialCategory category);
    Task<FinancialCategory> UpdateCategoryAsync(FinancialCategory category);
    Task<FinancialCategory> UpdateCategoryAsync(FinancialCategory category, long? scopeBranchId);
    Task SoftDeleteCategoryAsync(long id, long companyId);
    Task SoftDeleteCategoryAsync(long id, long companyId, long? branchId);

    Task<int> InitMonthFromFinancialItemsAsync(long companyId, long? branchId, DateTime monthStart, long? userId, IReadOnlyCollection<FinanceMonthSelectionItem>? selectedItems = null);

    Task<IEnumerable<FinancialEntry>> GetEntriesAsync(long companyId, long? branchId = null, string? type = null, long? categoryId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<FinancialEntry?> GetEntryByIdAsync(long id, long companyId);
    Task<FinancialEntry?> GetEntryByIdAsync(long id, long companyId, long? branchId);
    Task<FinancialEntry> CreateEntryAsync(FinancialEntry entry);
    Task<FinancialEntry> UpdateEntryAsync(FinancialEntry entry);
    Task<FinancialEntry> UpdateEntryAsync(FinancialEntry entry, long? scopeBranchId);
    Task SoftDeleteEntryAsync(long id, long companyId);
    Task SoftDeleteEntryAsync(long id, long companyId, long? branchId);
    Task<FinancialSummary> GetSummaryAsync(long companyId, long? branchId = null, DateTime? startDate = null, DateTime? endDate = null);
}
