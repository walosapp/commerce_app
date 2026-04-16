using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Walos.Application.DTOs.Admin;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<AdminRepository> _logger;

    public AdminRepository(IDbConnectionFactory db, ILogger<AdminRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> TaxIdExistsAsync(string taxId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = "SELECT COUNT(1) FROM core.companies WHERE tax_id = @TaxId AND deleted_at IS NULL";
        return await conn.ExecuteScalarAsync<int>(sql, new { TaxId = taxId }) > 0;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = "SELECT COUNT(1) FROM core.users WHERE email = @Email AND deleted_at IS NULL";
        return await conn.ExecuteScalarAsync<int>(sql, new { Email = email }) > 0;
    }

    public async Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            const string companySql = @"
                INSERT INTO core.companies (
                    name, legal_name, tax_id, email, phone,
                    address, city, state, country, postal_code,
                    currency, timezone, language, is_active
                ) VALUES (
                    @Name, @LegalName, @TaxId, @Email, @Phone,
                    @Address, @City, @State, @Country, @PostalCode,
                    @Currency, @Timezone, @Language, TRUE
                )
                RETURNING id AS Id, name AS Name, legal_name AS LegalName, tax_id AS TaxId,
                          email AS Email, phone AS Phone, city AS City, country AS Country,
                          currency AS Currency, language AS Language, is_active AS IsActive,
                          created_at AS CreatedAt";

            var company = await conn.QuerySingleAsync<TenantResponse>(companySql, new
            {
                Name = request.CompanyName,
                request.LegalName,
                request.TaxId,
                request.Email,
                request.Phone,
                request.Address,
                request.City,
                request.State,
                request.Country,
                request.PostalCode,
                request.Currency,
                request.Timezone,
                request.Language
            }, tx);

            const string branchSql = @"
                INSERT INTO core.branches (company_id, name, type, is_main, is_active)
                VALUES (@CompanyId, @Name, @Type, TRUE, TRUE)
                RETURNING id";

            var branchId = await conn.ExecuteScalarAsync<long>(branchSql, new
            {
                CompanyId = company.Id,
                Name = request.BranchName,
                Type = request.BranchType
            }, tx);

            const string rolesSql = @"
                INSERT INTO core.roles (company_id, name, code, is_system, is_active)
                VALUES
                    (@CompanyId, 'Super Admin', 'super_admin', TRUE, TRUE),
                    (@CompanyId, 'Gerente', 'manager', TRUE, TRUE),
                    (@CompanyId, 'Cajero', 'cashier', TRUE, TRUE),
                    (@CompanyId, 'Mesero', 'waiter', TRUE, TRUE)
                ON CONFLICT DO NOTHING
                RETURNING id, code";

            var roles = (await conn.QueryAsync<(long Id, string Code)>(
                "INSERT INTO core.roles (company_id, name, code, is_system, is_active) VALUES (@CompanyId, 'Super Admin', 'super_admin', TRUE, TRUE), (@CompanyId, 'Gerente', 'manager', TRUE, TRUE), (@CompanyId, 'Cajero', 'cashier', TRUE, TRUE), (@CompanyId, 'Mesero', 'waiter', TRUE, TRUE) RETURNING id, code",
                new { CompanyId = company.Id }, tx)).ToList();

            var superAdminRoleId = roles.FirstOrDefault(r => r.Code == "super_admin").Id;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword);
            const string userSql = @"
                INSERT INTO core.users (
                    company_id, branch_id, role_id,
                    first_name, last_name, email, password_hash,
                    language, is_active, email_verified
                ) VALUES (
                    @CompanyId, @BranchId, @RoleId,
                    @FirstName, @LastName, @Email, @PasswordHash,
                    @Language, TRUE, TRUE
                )
                RETURNING id";

            var adminUserId = await conn.ExecuteScalarAsync<long>(userSql, new
            {
                CompanyId = company.Id,
                BranchId = branchId,
                RoleId = superAdminRoleId,
                FirstName = request.AdminFirstName,
                LastName = request.AdminLastName,
                Email = request.AdminEmail,
                PasswordHash = passwordHash,
                Language = request.Language
            }, tx);

            const string invCatSql = @"
                INSERT INTO inventory.categories (company_id, name, is_active)
                VALUES
                    (@CompanyId, 'Bebidas', TRUE),
                    (@CompanyId, 'Alimentos', TRUE),
                    (@CompanyId, 'Licores', TRUE),
                    (@CompanyId, 'Insumos', TRUE)
                ON CONFLICT DO NOTHING";
            await conn.ExecuteAsync(invCatSql, new { CompanyId = company.Id }, tx);

            const string unitsSql = @"
                INSERT INTO inventory.units (company_id, name, abbreviation, is_active)
                VALUES
                    (@CompanyId, 'Unidad', 'und', TRUE),
                    (@CompanyId, 'Litro', 'lt', TRUE),
                    (@CompanyId, 'Kilogramo', 'kg', TRUE),
                    (@CompanyId, 'Gramo', 'gr', TRUE),
                    (@CompanyId, 'Mililitro', 'ml', TRUE),
                    (@CompanyId, 'Botella', 'bot', TRUE)
                ON CONFLICT DO NOTHING";
            await conn.ExecuteAsync(unitsSql, new { CompanyId = company.Id }, tx);

            const string finCatSql = @"
                INSERT INTO finance.categories (company_id, name, type, nature, frequency, is_system, is_active)
                VALUES
                    (@CompanyId, 'Ventas', 'income', 'operational', 'monthly', TRUE, TRUE),
                    (@CompanyId, 'Arriendo', 'expense', 'fixed', 'monthly', TRUE, TRUE),
                    (@CompanyId, 'Nomina', 'expense', 'fixed', 'monthly', TRUE, TRUE),
                    (@CompanyId, 'Servicios Publicos', 'expense', 'fixed', 'monthly', TRUE, TRUE),
                    (@CompanyId, 'Insumos', 'expense', 'variable', 'monthly', TRUE, TRUE)
                ON CONFLICT DO NOTHING";
            await conn.ExecuteAsync(finCatSql, new { CompanyId = company.Id }, tx);

            tx.Commit();

            _logger.LogInformation("Tenant creado: {CompanyName} (id={CompanyId})", request.CompanyName, company.Id);

            company.BranchCount = 1;
            company.UserCount = 1;

            return new CreateTenantResult
            {
                Company = company,
                AdminUserId = adminUserId,
                AdminEmail = request.AdminEmail,
                BranchId = branchId,
                BranchName = request.BranchName
            };
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<TenantResponse>> GetTenantsAsync()
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                c.id AS Id,
                c.name AS Name,
                c.legal_name AS LegalName,
                c.tax_id AS TaxId,
                c.email AS Email,
                c.phone AS Phone,
                c.city AS City,
                c.country AS Country,
                c.currency AS Currency,
                c.language AS Language,
                c.is_active AS IsActive,
                c.created_at AS CreatedAt,
                COUNT(DISTINCT b.id) AS BranchCount,
                COUNT(DISTINCT u.id) AS UserCount
            FROM core.companies c
            LEFT JOIN core.branches b ON b.company_id = c.id AND b.deleted_at IS NULL
            LEFT JOIN core.users u ON u.company_id = c.id AND u.deleted_at IS NULL
            WHERE c.deleted_at IS NULL
            GROUP BY c.id, c.name, c.legal_name, c.tax_id, c.email, c.phone,
                     c.city, c.country, c.currency, c.language, c.is_active, c.created_at
            ORDER BY c.created_at DESC";

        return await conn.QueryAsync<TenantResponse>(sql);
    }

    public async Task<TenantResponse?> GetTenantByIdAsync(long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                c.id AS Id, c.name AS Name, c.legal_name AS LegalName, c.tax_id AS TaxId,
                c.email AS Email, c.phone AS Phone, c.city AS City, c.country AS Country,
                c.currency AS Currency, c.language AS Language, c.is_active AS IsActive,
                c.created_at AS CreatedAt,
                COUNT(DISTINCT b.id) AS BranchCount,
                COUNT(DISTINCT u.id) AS UserCount
            FROM core.companies c
            LEFT JOIN core.branches b ON b.company_id = c.id AND b.deleted_at IS NULL
            LEFT JOIN core.users u ON u.company_id = c.id AND u.deleted_at IS NULL
            WHERE c.id = @CompanyId AND c.deleted_at IS NULL
            GROUP BY c.id, c.name, c.legal_name, c.tax_id, c.email, c.phone,
                     c.city, c.country, c.currency, c.language, c.is_active, c.created_at";

        return await conn.QueryFirstOrDefaultAsync<TenantResponse>(sql, new { CompanyId = companyId });
    }

    public async Task<bool> SetTenantActiveAsync(long companyId, bool isActive)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.companies
            SET is_active = @IsActive, updated_at = NOW()
            WHERE id = @CompanyId AND deleted_at IS NULL";
        var rows = await conn.ExecuteAsync(sql, new { CompanyId = companyId, IsActive = isActive });
        return rows > 0;
    }
}
