using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IAuthRepository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task UpdateLastLoginAsync(long userId, string? ipAddress);
    Task IncrementFailedLoginAsync(long userId);
    Task ResetFailedLoginAsync(long userId);
    Task LockUserAsync(long userId, DateTime lockedUntil);
    Task SaveRefreshTokenAsync(long userId, string refreshToken, DateTime expiresAt);
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
}
