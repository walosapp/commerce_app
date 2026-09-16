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
    Task<bool> SaveRefreshTokenAfterPasswordVerificationAsync(
        long userId,
        string expectedPasswordHash,
        string refreshToken,
        DateTime expiresAt);
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
    Task<bool> RotateRefreshTokenAsync(
        long userId,
        string expectedRefreshToken,
        string newRefreshToken,
        DateTime expiresAt);
    Task<User?> GetUserForPasswordChangeAsync(long userId, long companyId);
    Task<User?> GetUserForAccessValidationAsync(long userId, long companyId);
    Task<bool> ChangePasswordAndRotateRefreshTokenAsync(
        long userId,
        long companyId,
        string expectedPasswordHash,
        string newPasswordHash,
        string newRefreshToken,
        DateTime refreshTokenExpiresAt);
}
