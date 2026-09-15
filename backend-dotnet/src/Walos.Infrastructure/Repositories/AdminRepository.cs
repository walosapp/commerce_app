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

    public async Task<bool> AdminEmailExistsAsync(string email, long? excludeCompanyId = null)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT COUNT(1)
            FROM core.users u
            INNER JOIN core.roles r ON r.id = u.role_id AND r.company_id = u.company_id
            WHERE u.email = @Email
              AND r.code = 'super_admin'
              AND u.deleted_at IS NULL
              AND (@ExcludeCompanyId IS NULL OR u.company_id <> @ExcludeCompanyId)";

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            Email = email.Trim(),
            ExcludeCompanyId = excludeCompanyId
        }) > 0;
    }

    public async Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var legalName = string.IsNullOrWhiteSpace(request.LegalName)
                ? request.CompanyName
                : request.LegalName.Trim();
            var taxId = string.IsNullOrWhiteSpace(request.TaxId)
                ? $"TEMP-{Guid.NewGuid():N}"[..17]
                : request.TaxId.Trim();
            var branchType = string.IsNullOrWhiteSpace(request.BranchType)
                ? "general"
                : request.BranchType.Trim().ToLowerInvariant();
            var branchCode = "MAIN";
            var branchAddress = string.IsNullOrWhiteSpace(request.Address)
                ? "Por definir"
                : request.Address.Trim();
            var branchCity = string.IsNullOrWhiteSpace(request.City)
                ? "Por definir"
                : request.City.Trim();

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
                LegalName = legalName,
                TaxId = taxId,
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

            const string companyFeaturesSql = @"
                INSERT INTO platform.company_features
                    (company_id, feature_code, is_enabled)
                SELECT @CompanyId, f.code, f.default_enabled
                FROM platform.features f
                WHERE f.is_active = TRUE
                ON CONFLICT (company_id, feature_code) DO NOTHING";

            await conn.ExecuteAsync(companyFeaturesSql, new { CompanyId = company.Id }, tx);

            const string branchSql = @"
                INSERT INTO core.branches (
                    company_id, name, code, branch_type,
                    address, city, state, country, postal_code,
                    is_main, is_active
                )
                VALUES (
                    @CompanyId, @Name, @Code, @BranchType,
                    @Address, @City, @State, @Country, @PostalCode,
                    TRUE, TRUE
                )
                RETURNING id";

            var branchId = await conn.ExecuteScalarAsync<long>(branchSql, new
            {
                CompanyId = company.Id,
                Name = request.BranchName,
                Code = branchCode,
                BranchType = branchType,
                Address = branchAddress,
                City = branchCity,
                request.State,
                request.Country,
                request.PostalCode
            }, tx);

            var roles = (await conn.QueryAsync<(long Id, string Code)>(@"
                INSERT INTO core.roles (company_id, name, code, is_system_role, is_active)
                VALUES
                    (@CompanyId, 'Super Admin', 'super_admin', TRUE, TRUE),
                    (@CompanyId, 'Gerente', 'manager', TRUE, TRUE),
                    (@CompanyId, 'Cajero', 'cashier', TRUE, TRUE),
                    (@CompanyId, 'Mesero', 'waiter', TRUE, TRUE)
                RETURNING id, code",
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
                INSERT INTO inventory.categories (company_id, name, code, is_active)
                VALUES
                    (@CompanyId, 'Bebidas', 'BEBIDAS', TRUE),
                    (@CompanyId, 'Alimentos', 'ALIMENTOS', TRUE),
                    (@CompanyId, 'Licores', 'LICORES', TRUE),
                    (@CompanyId, 'Insumos', 'INSUMOS', TRUE)
                ON CONFLICT DO NOTHING";
            await conn.ExecuteAsync(invCatSql, new { CompanyId = company.Id }, tx);

            const string unitsSql = @"
                INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active)
                VALUES
                    (@CompanyId, 'Unidad', 'und', 'quantity', TRUE),
                    (@CompanyId, 'Litro', 'lt', 'volume', TRUE),
                    (@CompanyId, 'Kilogramo', 'kg', 'weight', TRUE),
                    (@CompanyId, 'Gramo', 'gr', 'weight', TRUE),
                    (@CompanyId, 'Mililitro', 'ml', 'volume', TRUE),
                    (@CompanyId, 'Botella', 'bot', 'quantity', TRUE)
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
                admin_user.email AS AdminEmail,
                (c.tax_id = 'WALOS-SYSTEM-001') AS IsSystem,
                c.is_active AS IsActive,
                c.created_at AS CreatedAt,
                COUNT(DISTINCT b.id) AS BranchCount,
                COUNT(DISTINCT u.id) AS UserCount
            FROM core.companies c
            LEFT JOIN core.branches b ON b.company_id = c.id AND b.deleted_at IS NULL
            LEFT JOIN core.users u ON u.company_id = c.id AND u.deleted_at IS NULL
            LEFT JOIN LATERAL (
                SELECT u2.email
                FROM core.users u2
                INNER JOIN core.roles r2 ON r2.id = u2.role_id AND r2.company_id = u2.company_id
                WHERE u2.company_id = c.id
                  AND r2.code = 'super_admin'
                  AND u2.deleted_at IS NULL
                ORDER BY u2.id
                LIMIT 1
            ) admin_user ON TRUE
            WHERE c.deleted_at IS NULL
            GROUP BY c.id, c.name, c.legal_name, c.tax_id, c.email, c.phone,
                     c.city, c.country, c.currency, c.language, admin_user.email, c.is_active, c.created_at
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
                c.currency AS Currency, c.language AS Language, admin_user.email AS AdminEmail, c.is_active AS IsActive,
                (c.tax_id = 'WALOS-SYSTEM-001') AS IsSystem,
                c.created_at AS CreatedAt,
                COUNT(DISTINCT b.id) AS BranchCount,
                COUNT(DISTINCT u.id) AS UserCount
            FROM core.companies c
            LEFT JOIN core.branches b ON b.company_id = c.id AND b.deleted_at IS NULL
            LEFT JOIN core.users u ON u.company_id = c.id AND u.deleted_at IS NULL
            LEFT JOIN LATERAL (
                SELECT u2.email
                FROM core.users u2
                INNER JOIN core.roles r2 ON r2.id = u2.role_id AND r2.company_id = u2.company_id
                WHERE u2.company_id = c.id
                  AND r2.code = 'super_admin'
                  AND u2.deleted_at IS NULL
                ORDER BY u2.id
                LIMIT 1
            ) admin_user ON TRUE
            WHERE c.id = @CompanyId AND c.deleted_at IS NULL
            GROUP BY c.id, c.name, c.legal_name, c.tax_id, c.email, c.phone,
                     c.city, c.country, c.currency, c.language, admin_user.email, c.is_active, c.created_at";

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

    public async Task<TenantResponse?> UpdateTenantAsync(long companyId, UpdateTenantRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();
        const string sql = @"
            UPDATE core.companies SET
                name        = COALESCE(@Name,       name),
                legal_name  = COALESCE(@LegalName,  legal_name),
                tax_id      = COALESCE(@TaxId,      tax_id),
                email       = COALESCE(@Email,      email),
                phone       = COALESCE(@Phone,      phone),
                city        = COALESCE(@City,       city),
                country     = COALESCE(@Country,    country),
                currency    = COALESCE(@Currency,   currency),
                language    = COALESCE(@Language,   language),
                updated_at  = NOW()
            WHERE id = @CompanyId AND deleted_at IS NULL";

        await conn.ExecuteAsync(sql, new
        {
            CompanyId  = companyId,
            request.Name,
            request.LegalName,
            request.TaxId,
            request.Email,
            request.Phone,
            request.City,
            request.Country,
            request.Currency,
            request.Language
        }, tx);

        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            const string adminSql = @"
                UPDATE core.users u
                SET email = @AdminEmail,
                    updated_at = NOW()
                FROM core.roles r
                WHERE u.role_id = r.id
                  AND r.code = 'super_admin'
                  AND u.company_id = @CompanyId
                  AND u.deleted_at IS NULL";

            await conn.ExecuteAsync(adminSql, new
            {
                CompanyId = companyId,
                AdminEmail = request.AdminEmail.Trim()
            }, tx);
        }

        tx.Commit();

        return await GetTenantByIdAsync(companyId);
    }

    public async Task<bool> ResetTenantAdminPasswordAsync(long companyId, string passwordHash)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE core.users u
            SET password_hash = @PasswordHash,
                updated_at    = NOW(),
                failed_login_attempts = 0,
                locked_until  = NULL
            FROM core.roles r
            WHERE u.role_id      = r.id
              AND r.code         = 'super_admin'
              AND u.company_id   = @CompanyId
              AND u.deleted_at   IS NULL";

        var rows = await conn.ExecuteAsync(sql, new { CompanyId = companyId, PasswordHash = passwordHash });
        return rows > 0;
    }

    public async Task<IReadOnlyList<BranchAdminResponse>> GetBranchesAsync(long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        var branches = await conn.QueryAsync<BranchAdminResponse>($@"
            {BranchSelect}
            WHERE b.company_id = @CompanyId
              AND b.deleted_at IS NULL
            ORDER BY b.is_main DESC, b.name, b.id",
            new { CompanyId = companyId });
        return branches.AsList();
    }

    public async Task<BranchAdminResponse> CreateBranchAsync(
        long companyId, CreateBranchAdminRequest request, long actorId)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        var lockedCompanyId = await conn.QuerySingleOrDefaultAsync<long?>(@"
            SELECT id FROM core.companies
            WHERE id = @CompanyId AND deleted_at IS NULL
            FOR UPDATE", new { CompanyId = companyId }, tx);
        if (!lockedCompanyId.HasValue)
            throw new Walos.Domain.Exceptions.NotFoundException("Comercio no encontrado");

        var duplicate = await conn.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT 1 FROM core.branches
                WHERE company_id = @CompanyId
                  AND code = @Code
                  AND deleted_at IS NULL)",
            new { CompanyId = companyId, request.Code }, tx);
        if (duplicate)
            throw new Walos.Domain.Exceptions.BusinessException(
                "Ya existe una sucursal con ese codigo", "branch_code_conflict");

        var id = await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO core.branches (
                company_id, name, code, branch_type, email, phone,
                address, city, state, country, postal_code,
                max_tables, max_capacity, is_main, is_active, created_by, updated_by)
            VALUES (
                @CompanyId, @Name, @Code, @BranchType, @Email, @Phone,
                @Address, @City, @State, @Country, @PostalCode,
                @MaxTables, @MaxCapacity, FALSE, TRUE, @ActorId, @ActorId)
            RETURNING id", new
        {
            CompanyId = companyId,
            request.Name,
            request.Code,
            request.BranchType,
            request.Email,
            request.Phone,
            request.Address,
            request.City,
            request.State,
            request.Country,
            request.PostalCode,
            request.MaxTables,
            request.MaxCapacity,
            ActorId = actorId
        }, tx);

        var branch = await QueryBranchAsync(conn, tx, companyId, id)
            ?? throw new InvalidOperationException("No fue posible cargar la sucursal creada");
        tx.Commit();
        return branch;
    }

    public async Task<BranchAdminResponse?> UpdateBranchAsync(
        long companyId, long branchId, UpdateBranchAdminRequest request, long actorId)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var lockedCompanyId = await conn.QuerySingleOrDefaultAsync<long?>(@"
            SELECT id FROM core.companies
            WHERE id = @CompanyId AND deleted_at IS NULL
            FOR UPDATE", new { CompanyId = companyId }, tx);
        if (!lockedCompanyId.HasValue)
            throw new Walos.Domain.Exceptions.NotFoundException("Comercio no encontrado");

        var current = await conn.QuerySingleOrDefaultAsync<BranchLockRow>(@"
            SELECT id AS Id, is_active AS IsActive
            FROM core.branches
            WHERE id = @BranchId
              AND company_id = @CompanyId
              AND deleted_at IS NULL
            FOR UPDATE", new { CompanyId = companyId, BranchId = branchId }, tx);
        if (current is null)
            return null;

        var duplicate = await conn.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT 1 FROM core.branches
                WHERE company_id = @CompanyId
                  AND code = @Code
                  AND id <> @BranchId
                  AND deleted_at IS NULL)",
            new { CompanyId = companyId, BranchId = branchId, request.Code }, tx);
        if (duplicate)
            throw new Walos.Domain.Exceptions.BusinessException(
                "Ya existe una sucursal con ese codigo", "branch_code_conflict");

        if (request.IsActive == false && current.IsActive)
        {
            var hasOtherActiveBranch = await conn.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT 1 FROM core.branches
                    WHERE company_id = @CompanyId
                      AND id <> @BranchId
                      AND is_active = TRUE
                      AND deleted_at IS NULL)",
                new { CompanyId = companyId, BranchId = branchId }, tx);

            if (!hasOtherActiveBranch)
                throw new Walos.Domain.Exceptions.BusinessException(
                    "No se puede desactivar la ultima sucursal activa del comercio",
                    "last_active_branch");

            var hasUnsafeOperations = await conn.ExecuteScalarAsync<bool>(@"
                SELECT
                    EXISTS (SELECT 1 FROM sales.cash_registers
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND status = 'open' AND deleted_at IS NULL)
                 OR EXISTS (SELECT 1 FROM sales.tables
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND status NOT IN ('invoiced', 'cancelled') AND deleted_at IS NULL)
                 OR EXISTS (SELECT 1 FROM sales.orders
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND status NOT IN ('completed', 'cancelled') AND deleted_at IS NULL)
                 OR EXISTS (SELECT 1 FROM delivery.orders
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND status NOT IN ('delivered', 'cancelled', 'rejected', 'returned')
                              AND deleted_at IS NULL)
                 OR EXISTS (SELECT 1 FROM suppliers.purchase_orders
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND status IN ('pending', 'ordered'))
                 OR EXISTS (SELECT 1 FROM sales.credits
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND status IN ('pending', 'partial'))
                 OR EXISTS (SELECT 1 FROM core.users
                            WHERE company_id = @CompanyId AND branch_id = @BranchId
                              AND is_active = TRUE AND deleted_at IS NULL)",
                new { CompanyId = companyId, BranchId = branchId }, tx);

            if (hasUnsafeOperations)
                throw new Walos.Domain.Exceptions.BusinessException(
                    "La sucursal tiene operaciones o usuarios activos",
                    "branch_has_active_operations");
        }

        await conn.ExecuteAsync(@"
            UPDATE core.branches SET
                name = @Name,
                code = @Code,
                branch_type = @BranchType,
                email = @Email,
                phone = @Phone,
                address = @Address,
                city = @City,
                state = @State,
                country = @Country,
                postal_code = @PostalCode,
                max_tables = @MaxTables,
                max_capacity = @MaxCapacity,
                is_active = @IsActive,
                updated_at = NOW(),
                updated_by = @ActorId
            WHERE id = @BranchId
              AND company_id = @CompanyId
              AND deleted_at IS NULL", new
        {
            CompanyId = companyId,
            BranchId = branchId,
            request.Name,
            request.Code,
            request.BranchType,
            request.Email,
            request.Phone,
            request.Address,
            request.City,
            request.State,
            request.Country,
            request.PostalCode,
            request.MaxTables,
            request.MaxCapacity,
            request.IsActive,
            ActorId = actorId
        }, tx);

        var branch = await QueryBranchAsync(conn, tx, companyId, branchId);
        tx.Commit();
        return branch;
    }

    private const string BranchSelect = @"
        SELECT b.id AS Id, b.company_id AS CompanyId, b.name AS Name, b.code AS Code,
               b.branch_type AS BranchType, b.email AS Email, b.phone AS Phone,
               b.address AS Address, b.city AS City, b.state AS State, b.country AS Country,
               b.postal_code AS PostalCode, b.max_tables AS MaxTables,
               b.max_capacity AS MaxCapacity, b.is_main AS IsMain, b.is_active AS IsActive,
               b.created_at AS CreatedAt, b.updated_at AS UpdatedAt
        FROM core.branches b";

    private static Task<BranchAdminResponse?> QueryBranchAsync(
        IDbConnection connection, IDbTransaction transaction, long companyId, long branchId)
        => connection.QuerySingleOrDefaultAsync<BranchAdminResponse>($@"
            {BranchSelect}
            WHERE b.company_id = @CompanyId
              AND b.id = @BranchId
              AND b.deleted_at IS NULL",
            new { CompanyId = companyId, BranchId = branchId }, transaction);

    private sealed class BranchLockRow
    {
        public long Id { get; init; }
        public bool IsActive { get; init; }
    }
}
