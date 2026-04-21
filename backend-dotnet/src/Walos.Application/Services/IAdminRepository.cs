using Walos.Application.DTOs.Admin;

namespace Walos.Application.Services;

public interface IAdminRepository
{
    Task<bool> TaxIdExistsAsync(string taxId);
    Task<bool> EmailExistsAsync(string email);
    Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request);
    Task<IEnumerable<TenantResponse>> GetTenantsAsync();
    Task<TenantResponse?> GetTenantByIdAsync(long companyId);
    Task<bool> SetTenantActiveAsync(long companyId, bool isActive);
    Task<TenantResponse?> UpdateTenantAsync(long companyId, UpdateTenantRequest request);
}
