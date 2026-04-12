using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class FinanceRepository : IFinanceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FinanceRepository> _logger;

    public FinanceRepository(IDbConnectionFactory connectionFactory, ILogger<FinanceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<FinancialCategory>> GetCategoriesAsync(long companyId, string? type = null)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var sql = @"
                SELECT
                    c.id AS Id,
                    c.company_id AS CompanyId,
                    c.name AS Name,
                    c.type AS Type,
                    c.color_hex AS ColorHex,
                    c.is_system AS IsSystem,
                    c.is_active AS IsActive,
                    c.created_by AS CreatedBy,
                    c.created_at AS CreatedAt,
                    c.updated_at AS UpdatedAt,
                    COUNT(e.id) AS EntryCount,
                    COALESCE(SUM(e.amount), 0) AS TotalAmount
                FROM finance.categories c
                LEFT JOIN finance.entries e ON e.category_id = c.id AND e.deleted_at IS NULL
                WHERE c.company_id = @CompanyId
                  AND c.deleted_at IS NULL
                  AND c.is_active = TRUE";

            var parameters = new DynamicParameters(new { CompanyId = companyId });
            if (!string.IsNullOrWhiteSpace(type))
            {
                sql += " AND c.type = @Type";
                parameters.Add("Type", type);
            }

            sql += @"
                GROUP BY c.id, c.company_id, c.name, c.type, c.color_hex, c.is_system, c.is_active, c.created_by, c.created_at, c.updated_at
                ORDER BY c.type ASC, c.name ASC";

            return await connection.QueryAsync<FinancialCategory>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo categorias financieras");
            throw;
        }
    }

    public async Task<FinancialCategory?> GetCategoryByIdAsync(long id, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, name AS Name, type AS Type,
                       color_hex AS ColorHex, is_system AS IsSystem, is_active AS IsActive,
                       created_by AS CreatedBy, created_at AS CreatedAt, updated_at AS UpdatedAt,
                       deleted_at AS DeletedAt
                FROM finance.categories
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<FinancialCategory>(sql, new { Id = id, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo categoria financiera {CategoryId}", id);
            throw;
        }
    }

    public async Task<FinancialCategory> CreateCategoryAsync(FinancialCategory category)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO finance.categories (company_id, name, type, color_hex, is_system, is_active, created_by)
                VALUES (@CompanyId, @Name, @Type, @ColorHex, @IsSystem, @IsActive, @CreatedBy)
                RETURNING id AS Id, company_id AS CompanyId, name AS Name,
                       type AS Type, color_hex AS ColorHex, is_system AS IsSystem,
                       is_active AS IsActive, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt";

            return await connection.QuerySingleAsync<FinancialCategory>(sql, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando categoria financiera");
            throw;
        }
    }

    public async Task<FinancialCategory> UpdateCategoryAsync(FinancialCategory category)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE finance.categories
                SET name = @Name,
                    type = @Type,
                    color_hex = @ColorHex,
                    updated_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL;

                SELECT id AS Id, company_id AS CompanyId, name AS Name, type AS Type,
                       color_hex AS ColorHex, is_system AS IsSystem, is_active AS IsActive,
                       created_by AS CreatedBy, created_at AS CreatedAt, updated_at AS UpdatedAt,
                       deleted_at AS DeletedAt
                FROM finance.categories
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL;";

            return await connection.QuerySingleAsync<FinancialCategory>(sql, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando categoria financiera {CategoryId}", category.Id);
            throw;
        }
    }

    public async Task SoftDeleteCategoryAsync(long id, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE finance.categories
                SET deleted_at = NOW(),
                    is_active = FALSE,
                    updated_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";

            await connection.ExecuteAsync(sql, new { Id = id, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando categoria financiera {CategoryId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<FinancialEntry>> GetEntriesAsync(long companyId, long? branchId = null, string? type = null, long? categoryId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var sql = @"
                SELECT
                    e.id AS Id,
                    e.company_id AS CompanyId,
                    e.branch_id AS BranchId,
                    e.category_id AS CategoryId,
                    c.name AS CategoryName,
                    b.name AS BranchName,
                    e.type AS Type,
                    e.description AS Description,
                    e.amount AS Amount,
                    e.entry_date AS EntryDate,
                    e.nature AS Nature,
                    e.frequency AS Frequency,
                    e.notes AS Notes,
                    e.created_by AS CreatedBy,
                    e.created_at AS CreatedAt,
                    e.updated_at AS UpdatedAt,
                    e.deleted_at AS DeletedAt
                FROM finance.entries e
                INNER JOIN finance.categories c ON c.id = e.category_id AND c.company_id = e.company_id
                LEFT JOIN core.branches b ON b.id = e.branch_id AND b.company_id = e.company_id
                WHERE e.company_id = @CompanyId
                  AND e.deleted_at IS NULL";

            var parameters = new DynamicParameters(new { CompanyId = companyId });

            if (branchId.HasValue)
            {
                sql += " AND e.branch_id = @BranchId";
                parameters.Add("BranchId", branchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                sql += " AND e.type = @Type";
                parameters.Add("Type", type);
            }

            if (categoryId.HasValue)
            {
                sql += " AND e.category_id = @CategoryId";
                parameters.Add("CategoryId", categoryId.Value);
            }

            if (startDate.HasValue)
            {
                sql += " AND e.entry_date >= @StartDate";
                parameters.Add("StartDate", startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                sql += " AND e.entry_date < @EndDate";
                parameters.Add("EndDate", endDate.Value.Date.AddDays(1));
            }

            sql += " ORDER BY e.entry_date DESC, e.created_at DESC";

            return await connection.QueryAsync<FinancialEntry>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo movimientos financieros");
            throw;
        }
    }

    public async Task<FinancialEntry?> GetEntryByIdAsync(long id, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId, category_id AS CategoryId,
                       type AS Type, description AS Description, amount AS Amount, entry_date AS EntryDate,
                       nature AS Nature, frequency AS Frequency, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, deleted_at AS DeletedAt
                FROM finance.entries
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<FinancialEntry>(sql, new { Id = id, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo movimiento financiero {EntryId}", id);
            throw;
        }
    }

    public async Task<FinancialEntry> CreateEntryAsync(FinancialEntry entry)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO finance.entries (
                    company_id, branch_id, category_id, type, description,
                    amount, entry_date, nature, frequency, notes, created_by
                ) VALUES (
                    @CompanyId, @BranchId, @CategoryId, @Type, @Description,
                    @Amount, @EntryDate, @Nature, @Frequency, @Notes, @CreatedBy
                )
                RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       category_id AS CategoryId, type AS Type, description AS Description,
                       amount AS Amount, entry_date AS EntryDate, nature AS Nature,
                       frequency AS Frequency, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt";

            return await connection.QuerySingleAsync<FinancialEntry>(sql, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando movimiento financiero");
            throw;
        }
    }

    public async Task<FinancialEntry> UpdateEntryAsync(FinancialEntry entry)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE finance.entries
                SET branch_id = @BranchId,
                    category_id = @CategoryId,
                    type = @Type,
                    description = @Description,
                    amount = @Amount,
                    entry_date = @EntryDate,
                    nature = @Nature,
                    frequency = @Frequency,
                    notes = @Notes,
                    updated_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL;

                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId, category_id AS CategoryId,
                       type AS Type, description AS Description, amount AS Amount, entry_date AS EntryDate,
                       nature AS Nature, frequency AS Frequency, notes AS Notes, created_by AS CreatedBy,
                       created_at AS CreatedAt, updated_at AS UpdatedAt, deleted_at AS DeletedAt
                FROM finance.entries
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL;";

            return await connection.QuerySingleAsync<FinancialEntry>(sql, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando movimiento financiero {EntryId}", entry.Id);
            throw;
        }
    }

    public async Task SoftDeleteEntryAsync(long id, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE finance.entries
                SET deleted_at = NOW(),
                    updated_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";

            await connection.ExecuteAsync(sql, new { Id = id, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando movimiento financiero {EntryId}", id);
            throw;
        }
    }

    public async Task<FinancialSummary> GetSummaryAsync(long companyId, long? branchId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var financeFilters = @"
                FROM finance.entries e
                INNER JOIN finance.categories c ON c.id = e.category_id AND c.company_id = e.company_id
                WHERE e.company_id = @CompanyId
                  AND e.deleted_at IS NULL";

            var salesFilters = @"
                FROM sales.orders o
                WHERE o.company_id = @CompanyId
                  AND o.status = 'completed'";

            var parameters = new DynamicParameters(new { CompanyId = companyId });

            if (branchId.HasValue)
            {
                financeFilters += " AND e.branch_id = @BranchId";
                salesFilters += " AND o.branch_id = @BranchId";
                parameters.Add("BranchId", branchId.Value);
            }

            if (startDate.HasValue)
            {
                financeFilters += " AND e.entry_date >= @StartDate";
                salesFilters += " AND COALESCE(o.updated_at, o.created_at) >= @StartDate";
                parameters.Add("StartDate", startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                financeFilters += " AND e.entry_date < @EndDate";
                salesFilters += " AND COALESCE(o.updated_at, o.created_at) < @EndDate";
                parameters.Add("EndDate", endDate.Value.Date.AddDays(1));
            }

            var summarySql = $@"
                SELECT
                    COALESCE(SUM(CASE WHEN e.type = 'income' THEN e.amount ELSE 0 END), 0) AS TotalIncome,
                    COALESCE(SUM(CASE WHEN e.type = 'expense' THEN e.amount ELSE 0 END), 0) AS TotalExpense,
                    0::DECIMAL(18,2) AS SystemSalesTotal,
                    0::DECIMAL(18,2) AS TotalBusinessIncome,
                    0::DECIMAL(18,2) AS NetBalance
                {financeFilters};

                SELECT
                    COALESCE(SUM(COALESCE(NULLIF(o.final_total_paid, 0), o.total)), 0) AS SystemSalesTotal
                {salesFilters};

                SELECT
                    c.name AS TopCategoryName,
                    COALESCE(SUM(e.amount), 0) AS TopCategoryAmount
                {financeFilters}
                GROUP BY c.name
                ORDER BY SUM(e.amount) DESC
                LIMIT 1;";

            using var multi = await connection.QueryMultipleAsync(summarySql, parameters);
            var summary = await multi.ReadFirstOrDefaultAsync<FinancialSummary>() ?? new FinancialSummary();
            var sales = await multi.ReadFirstOrDefaultAsync<FinancialSummary>();
            var topCategory = await multi.ReadFirstOrDefaultAsync<FinancialSummary>();

            summary.SystemSalesTotal = sales?.SystemSalesTotal ?? 0;
            summary.TotalBusinessIncome = summary.SystemSalesTotal + summary.TotalIncome;
            summary.NetBalance = summary.TotalBusinessIncome - summary.TotalExpense;

            if (topCategory is not null)
            {
                summary.TopCategoryName = topCategory.TopCategoryName;
                summary.TopCategoryAmount = topCategory.TopCategoryAmount;
            }

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo resumen financiero");
            throw;
        }
    }
}
