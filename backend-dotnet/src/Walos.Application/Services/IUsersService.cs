using Walos.Application.DTOs.Users;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IUsersService
{
    Task<IEnumerable<User>> GetAllAsync(long companyId);
    Task<IEnumerable<RoleOption>> GetRolesAsync(long companyId);
    Task<User?> GetByIdAsync(long id, long companyId);
    Task<User> CreateAsync(long companyId, long currentUserId, string currentRole, CreateUserRequest request);
    Task<User?> UpdateAsync(long companyId, long currentUserId, string currentRole, long id, UpdateUserRequest request);
    Task<bool> SetStatusAsync(long companyId, long currentUserId, string currentRole, long id, bool isActive);
    Task<bool> DeleteAsync(long companyId, long currentUserId, string currentRole, long id);
}
