using Walos.Application.Services;
using Walos.Application.DTOs.Users;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IUsersRepository
{
    Task<IEnumerable<User>> GetAllAsync(long companyId);
    Task<IEnumerable<User>> GetAllGlobalAsync(long? filterCompanyId = null);
    Task<User?> GetByIdAsync(long userId, long companyId);
    Task<User> CreateAsync(User user, string passwordHash);
    Task<User?> UpdateAsync(User user);
    Task<bool> SetActiveAsync(long userId, long companyId, bool isActive);
    Task<bool> SoftDeleteAsync(long userId, long companyId);
    Task<bool> EmailExistsAsync(string email, long? excludeUserId = null);
    Task<IEnumerable<RoleOption>> GetRolesAsync(long companyId, bool excludeDev = true);
    Task<RoleAssignmentInfo?> GetRoleForAssignmentAsync(long roleId, long companyId);
    Task<RoleAssignmentInfo?> GetUserRoleForAssignmentAsync(long userId, long companyId);
    Task<bool> IsActiveBranchInCompanyAsync(long branchId, long companyId);
    Task<bool> ResetPasswordByTenantActorAsync(
        long actorUserId,
        string actorRole,
        long targetUserId,
        long companyId,
        string newPasswordHash);
    Task<bool> ResetPasswordByPlatformActorAsync(
        long actorUserId,
        long targetUserId,
        long companyId,
        string newPasswordHash);
}

public sealed record RoleAssignmentInfo(long Id, string Code, int AccessLevel);

