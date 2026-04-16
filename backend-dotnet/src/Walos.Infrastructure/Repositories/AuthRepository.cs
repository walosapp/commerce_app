using Dapper;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _db;

    public AuthRepository(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            SELECT u.id AS Id,
                   u.company_id AS CompanyId,
                   u.branch_id AS BranchId,
                   u.role_id AS RoleId,
                   u.first_name AS FirstName,
                   u.last_name AS LastName,
                   u.email AS Email,
                   u.phone AS Phone,
                   u.password_hash AS PasswordHash,
                   u.refresh_token AS RefreshToken,
                   u.refresh_token_expires_at AS RefreshTokenExpiresAt,
                   u.language AS Language,
                   u.avatar_url AS AvatarUrl,
                   u.failed_login_attempts AS FailedLoginAttempts,
                   u.locked_until AS LockedUntil,
                   u.last_login_at AS LastLoginAt,
                   u.last_login_ip AS LastLoginIp,
                   u.is_active AS IsActive,
                   u.email_verified AS EmailVerified,
                   u.created_at AS CreatedAt,
                   r.code AS RoleCode,
                   r.name AS RoleName,
                   b.name AS BranchName,
                   c.name AS CompanyName
            FROM core.users u
            INNER JOIN core.roles r ON u.role_id = r.id AND r.company_id = u.company_id
            INNER JOIN core.companies c ON u.company_id = c.id
            LEFT JOIN core.branches b ON u.branch_id = b.id AND b.company_id = u.company_id
            WHERE u.email = @Email
              AND u.deleted_at IS NULL";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task UpdateLastLoginAsync(long userId, string? ipAddress)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            UPDATE core.users
            SET last_login_at = NOW(),
                last_login_ip = @IpAddress,
                updated_at = NOW()
            WHERE id = @UserId";

        await conn.ExecuteAsync(sql, new { UserId = userId, IpAddress = ipAddress });
    }

    public async Task IncrementFailedLoginAsync(long userId)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            UPDATE core.users
            SET failed_login_attempts = failed_login_attempts + 1,
                updated_at = NOW()
            WHERE id = @UserId";

        await conn.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task ResetFailedLoginAsync(long userId)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            UPDATE core.users
            SET failed_login_attempts = 0,
                locked_until = NULL,
                updated_at = NOW()
            WHERE id = @UserId";

        await conn.ExecuteAsync(sql, new { UserId = userId });
    }

    public async Task LockUserAsync(long userId, DateTime lockedUntil)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            UPDATE core.users
            SET locked_until = @LockedUntil,
                updated_at = NOW()
            WHERE id = @UserId";

        await conn.ExecuteAsync(sql, new { UserId = userId, LockedUntil = lockedUntil });
    }

    public async Task SaveRefreshTokenAsync(long userId, string refreshToken, DateTime expiresAt)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            UPDATE core.users
            SET refresh_token = @RefreshToken,
                refresh_token_expires_at = @ExpiresAt,
                updated_at = NOW()
            WHERE id = @UserId";

        await conn.ExecuteAsync(sql, new { UserId = userId, RefreshToken = refreshToken, ExpiresAt = expiresAt });
    }

    public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string sql = @"
            SELECT u.id AS Id,
                   u.company_id AS CompanyId,
                   u.branch_id AS BranchId,
                   u.role_id AS RoleId,
                   u.first_name AS FirstName,
                   u.last_name AS LastName,
                   u.email AS Email,
                   u.refresh_token AS RefreshToken,
                   u.refresh_token_expires_at AS RefreshTokenExpiresAt,
                   u.is_active AS IsActive,
                   r.code AS RoleCode,
                   r.name AS RoleName
            FROM core.users u
            INNER JOIN core.roles r ON u.role_id = r.id AND r.company_id = u.company_id
            WHERE u.refresh_token = @RefreshToken
              AND u.refresh_token_expires_at > NOW()
              AND u.deleted_at IS NULL
              AND u.is_active = TRUE";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { RefreshToken = refreshToken });
    }
}
