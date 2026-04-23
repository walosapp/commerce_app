using BCrypt.Net;
using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Users;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class UsersRepository : IUsersRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<UsersRepository> _logger;

    public UsersRepository(IDbConnectionFactory db, ILogger<UsersRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<User>> GetAllAsync(long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                u.id AS Id, u.company_id AS CompanyId, u.branch_id AS BranchId,
                u.role_id AS RoleId, u.first_name AS FirstName, u.last_name AS LastName,
                u.email AS Email, u.phone AS Phone, u.language AS Language,
                u.avatar_url AS AvatarUrl, u.is_active AS IsActive,
                u.email_verified AS EmailVerified, u.last_login_at AS LastLoginAt,
                u.created_at AS CreatedAt, u.updated_at AS UpdatedAt,
                r.code AS RoleCode, r.name AS RoleName,
                b.name AS BranchName
            FROM core.users u
            JOIN core.roles r ON r.id = u.role_id
            LEFT JOIN core.branches b ON b.id = u.branch_id
            WHERE u.company_id = @CompanyId
              AND u.deleted_at IS NULL
              AND r.code != 'dev'
            ORDER BY u.first_name, u.last_name";
        return await conn.QueryAsync<User>(sql, new { CompanyId = companyId });
    }

    public async Task<IEnumerable<User>> GetAllGlobalAsync(long? filterCompanyId = null)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                u.id AS Id, u.company_id AS CompanyId, u.branch_id AS BranchId,
                u.role_id AS RoleId, u.first_name AS FirstName, u.last_name AS LastName,
                u.email AS Email, u.phone AS Phone, u.language AS Language,
                u.avatar_url AS AvatarUrl, u.is_active AS IsActive,
                u.email_verified AS EmailVerified, u.last_login_at AS LastLoginAt,
                u.created_at AS CreatedAt, u.updated_at AS UpdatedAt,
                r.code AS RoleCode, r.name AS RoleName,
                b.name AS BranchName,
                c.name AS CompanyName
            FROM core.users u
            JOIN core.roles r ON r.id = u.role_id
            LEFT JOIN core.branches b ON b.id = u.branch_id
            JOIN core.companies c ON c.id = u.company_id
            WHERE u.deleted_at IS NULL
              AND r.code != 'dev'
              AND (@FilterCompanyId IS NULL OR u.company_id = @FilterCompanyId)
            ORDER BY c.name, u.first_name, u.last_name";
        return await conn.QueryAsync<User>(sql, new { FilterCompanyId = filterCompanyId });
    }

    public async Task<User?> GetByIdAsync(long userId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                u.id AS Id, u.company_id AS CompanyId, u.branch_id AS BranchId,
                u.role_id AS RoleId, u.first_name AS FirstName, u.last_name AS LastName,
                u.email AS Email, u.phone AS Phone, u.language AS Language,
                u.avatar_url AS AvatarUrl, u.is_active AS IsActive,
                u.email_verified AS EmailVerified, u.last_login_at AS LastLoginAt,
                u.created_at AS CreatedAt, u.updated_at AS UpdatedAt,
                r.code AS RoleCode, r.name AS RoleName,
                b.name AS BranchName
            FROM core.users u
            JOIN core.roles r ON r.id = u.role_id
            LEFT JOIN core.branches b ON b.id = u.branch_id
            WHERE u.id = @UserId AND u.company_id = @CompanyId AND u.deleted_at IS NULL";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId, CompanyId = companyId });
    }

    public async Task<User> CreateAsync(User user, string passwordHash)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO core.users (company_id, branch_id, role_id, first_name, last_name, email, phone, password_hash, is_active, created_by)
            VALUES (@CompanyId, @BranchId, @RoleId, @FirstName, @LastName, @Email, @Phone, @PasswordHash, TRUE, @CreatedBy)
            RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                      role_id AS RoleId, first_name AS FirstName, last_name AS LastName,
                      email AS Email, phone AS Phone, is_active AS IsActive,
                      email_verified AS EmailVerified, created_at AS CreatedAt, updated_at AS UpdatedAt";
        return await conn.QuerySingleAsync<User>(sql, new
        {
            user.CompanyId, user.BranchId, user.RoleId,
            user.FirstName, user.LastName, user.Email, user.Phone,
            PasswordHash = passwordHash,
            CreatedBy = user.CompanyId,
        });
    }

    public async Task<User?> UpdateAsync(User user)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users
            SET first_name = @FirstName, last_name = @LastName, phone = @Phone,
                role_id = @RoleId, branch_id = @BranchId, updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL
            RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                      role_id AS RoleId, first_name AS FirstName, last_name AS LastName,
                      email AS Email, phone AS Phone, is_active AS IsActive,
                      created_at AS CreatedAt, updated_at AS UpdatedAt";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, user);
    }

    public async Task<bool> SetActiveAsync(long userId, long companyId, bool isActive)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users SET is_active = @IsActive, updated_at = NOW()
            WHERE id = @UserId AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.ExecuteAsync(sql, new { UserId = userId, CompanyId = companyId, IsActive = isActive }) > 0;
    }

    public async Task<bool> SoftDeleteAsync(long userId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users SET deleted_at = NOW(), is_active = FALSE, updated_at = NOW()
            WHERE id = @UserId AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.ExecuteAsync(sql, new { UserId = userId, CompanyId = companyId }) > 0;
    }

    public async Task<bool> EmailExistsAsync(string email, long? excludeUserId = null)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT COUNT(1) FROM core.users
            WHERE email = @Email AND deleted_at IS NULL
              AND (@ExcludeId IS NULL OR id != @ExcludeId)";
        return await conn.ExecuteScalarAsync<int>(sql, new { Email = email, ExcludeId = excludeUserId }) > 0;
    }

    public async Task<bool> ResetPasswordAsync(long userId, long companyId, string newPasswordHash)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users SET password_hash = @Hash, updated_at = NOW()
            WHERE id = @UserId AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.ExecuteAsync(sql, new { Hash = newPasswordHash, UserId = userId, CompanyId = companyId }) > 0;
    }

    public async Task<IEnumerable<RoleOption>> GetRolesAsync(long companyId, bool excludeDev = true)
    {
        using var conn = await _db.CreateConnectionAsync();
        var where = excludeDev ? "AND r.code != 'dev'" : "";
        var sql = $@"
            SELECT id AS Id, code AS Code, name AS Name
            FROM core.roles r
            WHERE r.company_id = @CompanyId AND r.is_active = TRUE AND r.deleted_at IS NULL {where}
            ORDER BY r.access_level DESC, r.name ASC";
        return await conn.QueryAsync<RoleOption>(sql, new { CompanyId = companyId });
    }
}
