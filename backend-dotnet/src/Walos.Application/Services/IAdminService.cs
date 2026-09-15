using Walos.Application.DTOs.Admin;

namespace Walos.Application.Services;

public interface IAdminService
{
    Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request);
    Task<IEnumerable<TenantResponse>> GetTenantsAsync();
    Task<TenantResponse?> GetTenantByIdAsync(long companyId);
    Task<bool> SetTenantActiveAsync(long companyId, bool isActive);
    Task<TenantResponse?> UpdateTenantAsync(long companyId, UpdateTenantRequest request);
    Task ResetTenantAdminPasswordAsync(long companyId, string newPassword);
    Task<IReadOnlyList<BranchAdminResponse>> GetBranchesAsync(long companyId);
    Task<BranchAdminResponse> CreateBranchAsync(long companyId, CreateBranchAdminRequest request, long actorId);
    Task<BranchAdminResponse> UpdateBranchAsync(
        long companyId, long branchId, UpdateBranchAdminRequest request, long actorId);
}
