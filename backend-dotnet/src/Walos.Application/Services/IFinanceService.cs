using Walos.Application.DTOs.Finance;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IFinanceService
{
    Task<IEnumerable<FinancialCategory>> GetCategoriesAsync(long companyId, string? type = null);
    Task<FinancialCategory> CreateCategoryAsync(long companyId, long userId, long? branchId, CreateFinancialCategoryRequest request);
    Task<FinancialCategory> UpdateCategoryAsync(long companyId, long? branchId, long categoryId, UpdateFinancialCategoryRequest request);
    Task DeleteCategoryAsync(long companyId, long categoryId);
    Task<int> InitMonthAsync(long companyId, long userId, long? branchId, InitFinanceMonthRequest request);
    Task<IEnumerable<FinancialEntry>> GetEntriesAsync(long companyId, long? branchId, string? type, long? categoryId, DateTime? startDate, DateTime? endDate);
    Task<FinancialEntry> CreateEntryAsync(long companyId, long userId, long? branchId, CreateFinancialEntryRequest request);
    Task<FinancialEntry> UpdateEntryAsync(long companyId, long? branchId, long entryId, UpdateFinancialEntryRequest request);
    Task DeleteEntryAsync(long companyId, long entryId);
    Task<FinancialSummary> GetSummaryAsync(long companyId, long? branchId, DateTime? startDate, DateTime? endDate);
}
