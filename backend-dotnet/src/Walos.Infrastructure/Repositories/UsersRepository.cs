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
            JOIN core.roles r ON r.id = u.role_id AND r.company_id = u.company_id
            LEFT JOIN core.branches b ON b.id = u.branch_id AND b.company_id = u.company_id
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
            JOIN core.roles r ON r.id = u.role_id AND r.company_id = u.company_id
            LEFT JOIN core.branches b ON b.id = u.branch_id AND b.company_id = u.company_id
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
            JOIN core.roles r ON r.id = u.role_id AND r.company_id = u.company_id
            LEFT JOIN core.branches b ON b.id = u.branch_id AND b.company_id = u.company_id
            WHERE u.id = @UserId AND u.company_id = @CompanyId AND u.deleted_at IS NULL";
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId, CompanyId = companyId });
    }

    public async Task<User> CreateAsync(User user, string passwordHash)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO core.users (company_id, branch_id, role_id, first_name, last_name, email, phone, password_hash, is_active, created_by)
            SELECT @CompanyId, @BranchId, @RoleId, @FirstName, @LastName, @Email, @Phone, @PasswordHash, TRUE, @CreatedBy
            WHERE EXISTS (
                SELECT 1 FROM core.roles r
                WHERE r.id = @RoleId AND r.company_id = @CompanyId
                  AND r.code <> 'dev' AND r.is_active = TRUE AND r.deleted_at IS NULL
            )
              AND (
                  @BranchId IS NULL OR EXISTS (
                      SELECT 1 FROM core.branches b
                      WHERE b.id = @BranchId AND b.company_id = @CompanyId
                        AND b.is_active = TRUE AND b.deleted_at IS NULL
                  )
              )
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
            UPDATE core.users u
            SET first_name = @FirstName, last_name = @LastName, phone = @Phone,
                role_id = @RoleId, branch_id = @BranchId, updated_at = NOW()
            WHERE u.id = @Id AND u.company_id = @CompanyId AND u.deleted_at IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM core.roles r
                  WHERE r.id = u.role_id AND r.company_id = u.company_id
                    AND r.code = 'dev'
              )
              AND EXISTS (
                  SELECT 1 FROM core.roles r
                  WHERE r.id = @RoleId AND r.company_id = @CompanyId
                    AND r.code <> 'dev' AND r.is_active = TRUE AND r.deleted_at IS NULL
              )
              AND (
                  @BranchId IS NULL OR EXISTS (
                      SELECT 1 FROM core.branches b
                      WHERE b.id = @BranchId AND b.company_id = @CompanyId
                        AND b.is_active = TRUE AND b.deleted_at IS NULL
                  )
              )
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
            UPDATE core.users u SET is_active = @IsActive, updated_at = NOW()
            WHERE u.id = @UserId AND u.company_id = @CompanyId AND u.deleted_at IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM core.roles r
                  WHERE r.id = u.role_id AND r.company_id = u.company_id
                    AND r.code = 'dev'
              )";
        return await conn.ExecuteAsync(sql, new { UserId = userId, CompanyId = companyId, IsActive = isActive }) > 0;
    }

    public async Task<bool> SoftDeleteAsync(long userId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users u SET deleted_at = NOW(), is_active = FALSE, updated_at = NOW()
            WHERE u.id = @UserId AND u.company_id = @CompanyId AND u.deleted_at IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM core.roles r
                  WHERE r.id = u.role_id AND r.company_id = u.company_id
                    AND r.code = 'dev'
              )";
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

    public async Task<bool> ResetPasswordByTenantActorAsync(
        long actorUserId,
        string actorRole,
        long targetUserId,
        long companyId,
        string newPasswordHash)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users target
            SET password_hash = @Hash,
                refresh_token = NULL,
                refresh_token_expires_at = NULL,
                failed_login_attempts = 0,
                locked_until = NULL,
                updated_at = NOW()
            FROM core.users actor,
                 core.roles actor_role,
                 core.roles target_role
            WHERE target.id = @TargetUserId
              AND target.company_id = @CompanyId
              AND target.deleted_at IS NULL
              AND target.id <> @ActorUserId
              AND target.role_id = target_role.id
              AND target_role.company_id = target.company_id
              AND target_role.is_active = TRUE
              AND target_role.deleted_at IS NULL
              AND target_role.code <> 'dev'
              AND actor.id = @ActorUserId
              AND actor.company_id = @CompanyId
              AND actor.is_active = TRUE
              AND actor.deleted_at IS NULL
              AND actor.role_id = actor_role.id
              AND actor_role.company_id = actor.company_id
              AND actor_role.is_active = TRUE
              AND actor_role.deleted_at IS NULL
              AND actor_role.code = @ActorRole
              AND actor_role.code IN ('super_admin', 'manager', 'dev')
              AND NOT (actor_role.code = 'manager' AND target_role.code = 'super_admin')";
        return await conn.ExecuteAsync(sql, new
        {
            Hash = newPasswordHash,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            TargetUserId = targetUserId,
            CompanyId = companyId
        }) == 1;
    }

    public async Task<bool> ResetPasswordByPlatformActorAsync(
        long actorUserId,
        long targetUserId,
        long companyId,
        string newPasswordHash)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users target
            SET password_hash = @Hash,
                refresh_token = NULL,
                refresh_token_expires_at = NULL,
                failed_login_attempts = 0,
                locked_until = NULL,
                updated_at = NOW()
            FROM core.roles target_role
            WHERE target.id = @TargetUserId
              AND target.company_id = @CompanyId
              AND target.deleted_at IS NULL
              AND target.id <> @ActorUserId
              AND target.role_id = target_role.id
              AND target_role.company_id = target.company_id
              AND target_role.is_active = TRUE
              AND target_role.deleted_at IS NULL
              AND target_role.code <> 'dev'";
        return await conn.ExecuteAsync(sql, new
        {
            Hash = newPasswordHash,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            CompanyId = companyId
        }) == 1;
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

    public async Task<RoleAssignmentInfo?> GetRoleForAssignmentAsync(long roleId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT id AS Id, code AS Code, COALESCE(access_level, 1) AS AccessLevel
            FROM core.roles
            WHERE id = @RoleId
              AND company_id = @CompanyId
              AND is_active = TRUE
              AND deleted_at IS NULL";
        return await conn.QueryFirstOrDefaultAsync<RoleAssignmentInfo>(sql, new { RoleId = roleId, CompanyId = companyId });
    }

    public async Task<RoleAssignmentInfo?> GetUserRoleForAssignmentAsync(long userId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT r.id AS Id, r.code AS Code, COALESCE(r.access_level, 1) AS AccessLevel
            FROM core.users u
            INNER JOIN core.roles r ON r.id = u.role_id AND r.company_id = u.company_id
            WHERE u.id = @UserId
              AND u.company_id = @CompanyId
              AND u.is_active = TRUE
              AND u.deleted_at IS NULL
              AND r.is_active = TRUE
              AND r.deleted_at IS NULL";
        return await conn.QueryFirstOrDefaultAsync<RoleAssignmentInfo>(sql, new { UserId = userId, CompanyId = companyId });
    }

    public async Task<bool> IsActiveBranchInCompanyAsync(long branchId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM core.branches
                WHERE id = @BranchId
                  AND company_id = @CompanyId
                  AND is_active = TRUE
                  AND deleted_at IS NULL
            )";
        return await conn.ExecuteScalarAsync<bool>(sql, new { BranchId = branchId, CompanyId = companyId });
    }
}
