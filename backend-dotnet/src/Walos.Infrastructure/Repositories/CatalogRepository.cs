using Dapper;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class CatalogRepository : ICatalogRepository
{
    private readonly IDbConnectionFactory _db;
    public CatalogRepository(IDbConnectionFactory db) => _db = db;

    // ─── CATEGORIES ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<CategoryResponse>> GetCategoriesAsync(long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                c.id AS Id, c.company_id AS CompanyId,
                c.name AS Name, c.code AS Code, c.description AS Description,
                c.icon AS Icon, c.color AS Color,
                c.display_order AS DisplayOrder, c.is_active AS IsActive,
                c.created_at AS CreatedAt,
                COUNT(p.id) AS ProductCount
            FROM inventory.categories c
            LEFT JOIN inventory.products p
                ON p.category_id = c.id AND p.deleted_at IS NULL
            WHERE c.company_id = @CompanyId AND c.deleted_at IS NULL
            GROUP BY c.id
            ORDER BY c.display_order, c.name";
        return await conn.QueryAsync<CategoryResponse>(sql, new { CompanyId = companyId });
    }

    public async Task<CategoryResponse?> GetCategoryByIdAsync(long id, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT id AS Id, company_id AS CompanyId, name AS Name, code AS Code,
                   description AS Description, icon AS Icon, color AS Color,
                   display_order AS DisplayOrder, is_active AS IsActive, created_at AS CreatedAt
            FROM inventory.categories
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.QueryFirstOrDefaultAsync<CategoryResponse>(sql, new { Id = id, CompanyId = companyId });
    }

    public async Task<CategoryResponse> CreateCategoryAsync(long companyId, SaveCategoryRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? request.Name.ToUpperInvariant().Replace(" ", "_")[..Math.Min(request.Name.Length, 30)]
            : request.Code.Trim().ToUpperInvariant();

        const string sql = @"
            INSERT INTO inventory.categories (company_id, name, code, description, icon, color, display_order, is_active)
            VALUES (@CompanyId, @Name, @Code, @Description, @Icon, @Color, @DisplayOrder, TRUE)
            RETURNING id AS Id, company_id AS CompanyId, name AS Name, code AS Code,
                      description AS Description, icon AS Icon, color AS Color,
                      display_order AS DisplayOrder, is_active AS IsActive, created_at AS CreatedAt";
        return await conn.QuerySingleAsync<CategoryResponse>(sql, new
        {
            CompanyId = companyId,
            request.Name,
            Code = code,
            request.Description,
            request.Icon,
            request.Color,
            request.DisplayOrder
        });
    }

    public async Task<CategoryResponse?> UpdateCategoryAsync(long id, long companyId, SaveCategoryRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE inventory.categories
            SET name = @Name, description = @Description, icon = @Icon,
                color = @Color, display_order = @DisplayOrder, updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL
            RETURNING id AS Id, company_id AS CompanyId, name AS Name, code AS Code,
                      description AS Description, icon AS Icon, color AS Color,
                      display_order AS DisplayOrder, is_active AS IsActive, created_at AS CreatedAt";
        return await conn.QueryFirstOrDefaultAsync<CategoryResponse>(sql, new
        {
            Id = id, CompanyId = companyId,
            request.Name, request.Description, request.Icon, request.Color, request.DisplayOrder
        });
    }

    public async Task<bool> SetCategoryActiveAsync(long id, long companyId, bool isActive)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE inventory.categories SET is_active = @IsActive, updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.ExecuteAsync(sql, new { Id = id, CompanyId = companyId, IsActive = isActive }) > 0;
    }

    public async Task<bool> DeleteCategoryAsync(long id, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        // Only allow if no products use it
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM inventory.products WHERE category_id = @Id AND deleted_at IS NULL",
            new { Id = id });
        if (count > 0) throw new InvalidOperationException($"La categoria tiene {count} producto(s) asociados. Reasignalos antes de eliminarla.");

        const string sql = "UPDATE inventory.categories SET deleted_at = NOW() WHERE id = @Id AND company_id = @CompanyId";
        return await conn.ExecuteAsync(sql, new { Id = id, CompanyId = companyId }) > 0;
    }

    // ─── UNITS ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<UnitResponse>> GetUnitsAsync(long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                u.id AS Id, u.company_id AS CompanyId,
                u.name AS Name, u.abbreviation AS Abbreviation,
                u.unit_type AS UnitType, u.is_active AS IsActive,
                u.created_at AS CreatedAt,
                COUNT(p.id) AS ProductCount
            FROM inventory.units u
            LEFT JOIN inventory.products p
                ON p.unit_id = u.id AND p.deleted_at IS NULL
            WHERE u.company_id = @CompanyId AND u.deleted_at IS NULL
            GROUP BY u.id
            ORDER BY u.unit_type, u.name";
        return await conn.QueryAsync<UnitResponse>(sql, new { CompanyId = companyId });
    }

    public async Task<UnitResponse?> GetUnitByIdAsync(long id, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT id AS Id, company_id AS CompanyId, name AS Name,
                   abbreviation AS Abbreviation, unit_type AS UnitType,
                   is_active AS IsActive, created_at AS CreatedAt
            FROM inventory.units
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.QueryFirstOrDefaultAsync<UnitResponse>(sql, new { Id = id, CompanyId = companyId });
    }

    public async Task<UnitResponse> CreateUnitAsync(long companyId, SaveUnitRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active)
            VALUES (@CompanyId, @Name, @Abbreviation, @UnitType, TRUE)
            ON CONFLICT (company_id, abbreviation) DO NOTHING
            RETURNING id AS Id, company_id AS CompanyId, name AS Name,
                      abbreviation AS Abbreviation, unit_type AS UnitType,
                      is_active AS IsActive, created_at AS CreatedAt";
        var result = await conn.QueryFirstOrDefaultAsync<UnitResponse>(sql, new
        {
            CompanyId = companyId,
            request.Name,
            Abbreviation = request.Abbreviation.Trim().ToLowerInvariant(),
            request.UnitType
        });
        if (result is null)
            throw new InvalidOperationException($"La abreviatura '{request.Abbreviation}' ya existe.");
        return result;
    }

    public async Task<UnitResponse?> UpdateUnitAsync(long id, long companyId, SaveUnitRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE inventory.units
            SET name = @Name, unit_type = @UnitType, updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL
            RETURNING id AS Id, company_id AS CompanyId, name AS Name,
                      abbreviation AS Abbreviation, unit_type AS UnitType,
                      is_active AS IsActive, created_at AS CreatedAt";
        return await conn.QueryFirstOrDefaultAsync<UnitResponse>(sql, new
        {
            Id = id, CompanyId = companyId, request.Name, request.UnitType
        });
    }

    public async Task<bool> SetUnitActiveAsync(long id, long companyId, bool isActive)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = "UPDATE inventory.units SET is_active = @IsActive, updated_at = NOW() WHERE id = @Id AND company_id = @CompanyId";
        return await conn.ExecuteAsync(sql, new { Id = id, CompanyId = companyId, IsActive = isActive }) > 0;
    }

    public async Task<bool> DeleteUnitAsync(long id, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM inventory.products WHERE unit_id = @Id AND deleted_at IS NULL",
            new { Id = id });
        if (count > 0) throw new InvalidOperationException($"La unidad tiene {count} producto(s) asociados. Reasignalos antes de eliminarla.");

        const string sql = "UPDATE inventory.units SET deleted_at = NOW() WHERE id = @Id AND company_id = @CompanyId";
        return await conn.ExecuteAsync(sql, new { Id = id, CompanyId = companyId }) > 0;
    }
}
