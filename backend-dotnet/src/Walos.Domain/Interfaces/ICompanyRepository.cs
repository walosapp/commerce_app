using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ICompanyRepository
{
    Task<CompanySettings?> GetCompanySettingsAsync(long companyId);
    Task<CompanyOperationsSettings?> GetCompanyOperationsSettingsAsync(long companyId);
    Task<CompanySettings> UpdateCompanySettingsAsync(CompanySettings settings);
    Task<CompanyOperationsSettings> UpdateCompanyOperationsSettingsAsync(CompanyOperationsSettings settings);
    Task<bool> CompareExchangeCompanyLogoAsync(
        long companyId,
        string? expectedLogoReference,
        string? newLogoReference,
        long updatedBy);
}
