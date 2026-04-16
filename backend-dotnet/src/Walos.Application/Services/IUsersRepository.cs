using Walos.Application.DTOs.Users;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IUsersRepository
{
    Task<IEnumerable<User>> GetAllAsync(long companyId);
    Task<User?> GetByIdAsync(long userId, long companyId);
    Task<User> CreateAsync(User user, string passwordHash);
    Task<User?> UpdateAsync(User user);
    Task<bool> SetActiveAsync(long userId, long companyId, bool isActive);
    Task<bool> SoftDeleteAsync(long userId, long companyId);
    Task<bool> EmailExistsAsync(string email, long? excludeUserId = null);
    Task<IEnumerable<RoleOption>> GetRolesAsync(long companyId, bool excludeDev = true);
}
