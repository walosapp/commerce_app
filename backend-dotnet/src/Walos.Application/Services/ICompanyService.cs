using Walos.Application.DTOs.Company;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface ICompanyService
{
    Task<CompanySettings> GetSettingsAsync(long companyId);
    Task<CompanySettings> UpdateSettingsAsync(long companyId, long userId, UpdateCompanySettingsRequest request);
    Task<CompanyOperationsSettings> GetOperationsSettingsAsync(long companyId);
    Task<CompanyOperationsSettings> UpdateOperationsSettingsAsync(long companyId, UpdateCompanyOperationsSettingsRequest request);
    Task<string> UploadLogoAsync(long companyId, long userId, Stream fileStream, string fileName, string contentType);
    Task RemoveLogoAsync(long companyId, long userId);
}
