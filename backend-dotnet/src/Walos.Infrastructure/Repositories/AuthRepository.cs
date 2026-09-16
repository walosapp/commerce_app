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
                    c.name AS CompanyName,
                    c.tax_id AS CompanyTaxId
             FROM core.users u
             INNER JOIN core.roles r ON u.role_id = r.id
                 AND r.company_id = u.company_id
                 AND r.is_active = TRUE
                 AND r.deleted_at IS NULL
             INNER JOIN core.companies c ON u.company_id = c.id
                 AND c.is_active = TRUE
                 AND c.deleted_at IS NULL
             LEFT JOIN core.branches b ON u.branch_id = b.id
                 AND b.company_id = u.company_id
                 AND b.is_active = TRUE
                 AND b.deleted_at IS NULL
             WHERE u.email = @Email
               AND u.deleted_at IS NULL
               AND (u.branch_id IS NULL OR b.id IS NOT NULL)";

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

    public async Task<bool> SaveRefreshTokenAfterPasswordVerificationAsync(
        long userId,
        string expectedPasswordHash,
        string refreshToken,
        DateTime expiresAt)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users
            SET refresh_token = @RefreshToken,
                refresh_token_expires_at = @ExpiresAt,
                updated_at = NOW()
            WHERE id = @UserId
              AND password_hash = @ExpectedPasswordHash
              AND is_active = TRUE
              AND deleted_at IS NULL";

        return await conn.ExecuteAsync(sql, new
        {
            UserId = userId,
            ExpectedPasswordHash = expectedPasswordHash,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        }) == 1;
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
                   u.password_hash AS PasswordHash,
                   u.refresh_token AS RefreshToken,
                   u.refresh_token_expires_at AS RefreshTokenExpiresAt,
                    u.is_active AS IsActive,
                    r.code AS RoleCode,
                    r.name AS RoleName,
                    c.name AS CompanyName,
                    c.tax_id AS CompanyTaxId,
                    b.name AS BranchName
             FROM core.users u
             INNER JOIN core.roles r ON u.role_id = r.id
                 AND r.company_id = u.company_id
                 AND r.is_active = TRUE
                 AND r.deleted_at IS NULL
             INNER JOIN core.companies c ON u.company_id = c.id
                 AND c.is_active = TRUE
                 AND c.deleted_at IS NULL
             LEFT JOIN core.branches b ON u.branch_id = b.id
                 AND b.company_id = u.company_id
                 AND b.is_active = TRUE
                 AND b.deleted_at IS NULL
             WHERE u.refresh_token = @RefreshToken
               AND u.refresh_token_expires_at > NOW()
               AND u.deleted_at IS NULL
               AND u.is_active = TRUE
               AND (u.branch_id IS NULL OR b.id IS NOT NULL)";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { RefreshToken = refreshToken });
    }

    public async Task<bool> RotateRefreshTokenAsync(
        long userId,
        string expectedRefreshToken,
        string newRefreshToken,
        DateTime expiresAt)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users
            SET refresh_token = @NewRefreshToken,
                refresh_token_expires_at = @ExpiresAt,
                updated_at = NOW()
            WHERE id = @UserId
              AND refresh_token = @ExpectedRefreshToken
              AND refresh_token_expires_at > NOW()
              AND is_active = TRUE
              AND deleted_at IS NULL";

        return await conn.ExecuteAsync(sql, new
        {
            UserId = userId,
            ExpectedRefreshToken = expectedRefreshToken,
            NewRefreshToken = newRefreshToken,
            ExpiresAt = expiresAt
        }) == 1;
    }

    public async Task<User?> GetUserForPasswordChangeAsync(long userId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT u.id AS Id,
                   u.company_id AS CompanyId,
                   u.branch_id AS BranchId,
                   u.first_name AS FirstName,
                   u.last_name AS LastName,
                   u.email AS Email,
                   u.password_hash AS PasswordHash,
                   u.is_active AS IsActive,
                   u.language AS Language,
                   u.avatar_url AS AvatarUrl,
                   r.code AS RoleCode,
                   r.name AS RoleName,
                   b.name AS BranchName,
                   c.name AS CompanyName,
                   c.tax_id AS CompanyTaxId
            FROM core.users u
            INNER JOIN core.roles r ON r.id = u.role_id
                AND r.company_id = u.company_id
                AND r.is_active = TRUE
                AND r.deleted_at IS NULL
            INNER JOIN core.companies c ON c.id = u.company_id
                AND c.is_active = TRUE
                AND c.deleted_at IS NULL
            LEFT JOIN core.branches b ON b.id = u.branch_id
                AND b.company_id = u.company_id
                AND b.is_active = TRUE
                AND b.deleted_at IS NULL
            WHERE u.id = @UserId
              AND u.company_id = @CompanyId
              AND u.is_active = TRUE
              AND u.deleted_at IS NULL
              AND (u.branch_id IS NULL OR b.id IS NOT NULL)";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId, CompanyId = companyId });
    }

    public async Task<User?> GetUserForAccessValidationAsync(long userId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT u.id AS Id,
                   u.company_id AS CompanyId,
                   u.branch_id AS BranchId,
                   u.password_hash AS PasswordHash,
                   u.is_active AS IsActive,
                   r.code AS RoleCode,
                   c.tax_id AS CompanyTaxId
            FROM core.users u
            INNER JOIN core.roles r ON r.id = u.role_id
                AND r.company_id = u.company_id
                AND r.is_active = TRUE
                AND r.deleted_at IS NULL
            INNER JOIN core.companies c ON c.id = u.company_id
                AND c.is_active = TRUE
                AND c.deleted_at IS NULL
            LEFT JOIN core.branches b ON b.id = u.branch_id
                AND b.company_id = u.company_id
                AND b.is_active = TRUE
                AND b.deleted_at IS NULL
            WHERE u.id = @UserId
              AND u.company_id = @CompanyId
              AND u.is_active = TRUE
              AND u.deleted_at IS NULL
              AND (u.branch_id IS NULL OR b.id IS NOT NULL)";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId, CompanyId = companyId });
    }

    public async Task<bool> ChangePasswordAndRotateRefreshTokenAsync(
        long userId,
        long companyId,
        string expectedPasswordHash,
        string newPasswordHash,
        string newRefreshToken,
        DateTime refreshTokenExpiresAt)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users
            SET password_hash = @NewPasswordHash,
                refresh_token = @NewRefreshToken,
                refresh_token_expires_at = @RefreshTokenExpiresAt,
                updated_at = NOW()
            WHERE id = @UserId
              AND company_id = @CompanyId
              AND password_hash = @ExpectedPasswordHash
              AND is_active = TRUE
              AND deleted_at IS NULL";

        return await conn.ExecuteAsync(sql, new
        {
            UserId = userId,
            CompanyId = companyId,
            ExpectedPasswordHash = expectedPasswordHash,
            NewPasswordHash = newPasswordHash,
            NewRefreshToken = newRefreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        }) == 1;
    }
}
