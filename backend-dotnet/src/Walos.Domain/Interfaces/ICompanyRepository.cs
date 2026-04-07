using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ICompanyRepository
{
    Task<CompanySettings?> GetCompanySettingsAsync(long companyId);
    Task<CompanyOperationsSettings?> GetCompanyOperationsSettingsAsync(long companyId);
    Task<CompanySettings> UpdateCompanySettingsAsync(CompanySettings settings);
    Task<CompanyOperationsSettings> UpdateCompanyOperationsSettingsAsync(CompanyOperationsSettings settings);
    Task UpdateCompanyLogoAsync(long companyId, string logoUrl, long updatedBy);
}
