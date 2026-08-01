using Walos.Application.DTOs.Users;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IUsersService
{
    Task<IEnumerable<User>> GetAllAsync(long companyId);
    Task<IEnumerable<RoleOption>> GetRolesAsync(long companyId);
    Task<User?> GetByIdAsync(long id, long companyId);
    Task<User> CreateAsync(long companyId, CreateUserRequest request);
    Task<User?> UpdateAsync(long companyId, long id, UpdateUserRequest request);
    Task<bool> SetStatusAsync(long companyId, long currentUserId, long id, bool isActive);
    Task<bool> DeleteAsync(long companyId, long currentUserId, long id);
}
