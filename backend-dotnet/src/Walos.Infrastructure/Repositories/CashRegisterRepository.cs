using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class CashRegisterRepository : ICashRegisterRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CashRegisterRepository> _logger;

    public CashRegisterRepository(IDbConnectionFactory connectionFactory, ILogger<CashRegisterRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<CashRegister> OpenAsync(CashRegister register)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.cash_registers (
                company_id, branch_id, opened_by, status, opening_amount,
                total_sales, total_cash_sales, total_card_sales, total_transfer_sales, total_other_sales,
                total_discounts, total_credits, total_tips, cash_in, cash_out, order_count,
                notes, opened_at, created_at, updated_at
            ) VALUES (
                @CompanyId, @BranchId, @OpenedBy, @Status, @OpeningAmount,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                @Notes, @OpenedAt, NOW(), NOW()
            )
            RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId, opened_by AS OpenedBy,
                      status AS Status, opening_amount AS OpeningAmount,
                      total_sales AS TotalSales, total_cash_sales AS TotalCashSales,
                      total_card_sales AS TotalCardSales, total_transfer_sales AS TotalTransferSales,
                      total_other_sales AS TotalOtherSales, total_discounts AS TotalDiscounts,
                      total_credits AS TotalCredits, total_tips AS TotalTips,
                      cash_in AS CashIn, cash_out AS CashOut, order_count AS OrderCount,
                      notes AS Notes, opened_at AS OpenedAt, created_at AS CreatedAt";

        var result = await connection.QuerySingleAsync<CashRegister>(sql, new
        {
            register.CompanyId,
            register.BranchId,
            register.OpenedBy,
            register.Status,
            register.OpeningAmount,
            register.Notes,
            register.OpenedAt
        });

        _logger.LogInformation("Caja abierta: Id={Id}, Company={CompanyId}, Branch={BranchId}, User={OpenedBy}, Amount={OpeningAmount}",
            result.Id, result.CompanyId, result.BranchId, result.OpenedBy, result.OpeningAmount);

        return result;
    }

    public async Task<CashRegister?> GetActiveByUserAsync(long companyId, long branchId, long userId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                cr.id AS Id, cr.company_id AS CompanyId, cr.branch_id AS BranchId,
                cr.opened_by AS OpenedBy, cr.closed_by AS ClosedBy,
                cr.status AS Status, cr.opening_amount AS OpeningAmount,
                cr.closing_amount AS ClosingAmount, cr.expected_cash AS ExpectedCash,
                cr.difference AS Difference, cr.total_sales AS TotalSales,
                cr.total_cash_sales AS TotalCashSales, cr.total_card_sales AS TotalCardSales,
                cr.total_transfer_sales AS TotalTransferSales, cr.total_other_sales AS TotalOtherSales,
                cr.total_discounts AS TotalDiscounts, cr.total_credits AS TotalCredits,
                cr.total_tips AS TotalTips, cr.cash_in AS CashIn, cr.cash_out AS CashOut,
                cr.order_count AS OrderCount, cr.notes AS Notes,
                cr.opened_at AS OpenedAt, cr.closed_at AS ClosedAt,
                u.first_name || ' ' || u.last_name AS OpenedByName
            FROM sales.cash_registers cr
            LEFT JOIN core.users u ON cr.opened_by = u.id
            WHERE cr.company_id = @CompanyId 
              AND cr.branch_id = @BranchId
              AND cr.opened_by = @UserId
              AND cr.status = 'open'
              AND cr.deleted_at IS NULL";

        return await connection.QueryFirstOrDefaultAsync<CashRegister>(sql, new { CompanyId = companyId, BranchId = branchId, UserId = userId });
    }

    public async Task<CashRegister?> GetByIdAsync(long id, long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                cr.id AS Id, cr.company_id AS CompanyId, cr.branch_id AS BranchId,
                cr.opened_by AS OpenedBy, cr.closed_by AS ClosedBy,
                cr.status AS Status, cr.opening_amount AS OpeningAmount,
                cr.closing_amount AS ClosingAmount, cr.expected_cash AS ExpectedCash,
                cr.difference AS Difference, cr.total_sales AS TotalSales,
                cr.total_cash_sales AS TotalCashSales, cr.total_card_sales AS TotalCardSales,
                cr.total_transfer_sales AS TotalTransferSales, cr.total_other_sales AS TotalOtherSales,
                cr.total_discounts AS TotalDiscounts, cr.total_credits AS TotalCredits,
                cr.total_tips AS TotalTips, cr.cash_in AS CashIn, cr.cash_out AS CashOut,
                cr.order_count AS OrderCount, cr.notes AS Notes,
                cr.opened_at AS OpenedAt, cr.closed_at AS ClosedAt,
                uo.first_name || ' ' || uo.last_name AS OpenedByName,
                uc.first_name || ' ' || uc.last_name AS ClosedByName
            FROM sales.cash_registers cr
            LEFT JOIN core.users uo ON cr.opened_by = uo.id
            LEFT JOIN core.users uc ON cr.closed_by = uc.id
            WHERE cr.id = @Id AND cr.company_id = @CompanyId AND cr.deleted_at IS NULL";

        return await connection.QueryFirstOrDefaultAsync<CashRegister>(sql, new { Id = id, CompanyId = companyId });
    }

    public async Task<CashRegister> CloseAsync(long id, long companyId, long closedBy, decimal closingAmount, string? notes)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            UPDATE sales.cash_registers SET
                status = 'closed',
                closed_by = @ClosedBy,
                closing_amount = @ClosingAmount,
                expected_cash = (opening_amount + total_cash_sales + cash_in - cash_out - total_credits),
                difference = @ClosingAmount - (opening_amount + total_cash_sales + cash_in - cash_out - total_credits),
                notes = COALESCE(notes, '') || ' | ' || @Notes,
                closed_at = NOW(),
                updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND status = 'open'
            RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                      opened_by AS OpenedBy, closed_by AS ClosedBy,
                      status AS Status, opening_amount AS OpeningAmount,
                      closing_amount AS ClosingAmount, expected_cash AS ExpectedCash,
                      difference AS Difference, total_sales AS TotalSales,
                      total_cash_sales AS TotalCashSales, total_card_sales AS TotalCardSales,
                      total_transfer_sales AS TotalTransferSales, total_other_sales AS TotalOtherSales,
                      total_discounts AS TotalDiscounts, total_credits AS TotalCredits,
                      total_tips AS TotalTips, cash_in AS CashIn, cash_out AS CashOut,
                      order_count AS OrderCount, notes AS Notes,
                      opened_at AS OpenedAt, closed_at AS ClosedAt";

        var result = await connection.QuerySingleAsync<CashRegister>(sql, new
        {
            Id = id,
            CompanyId = companyId,
            ClosedBy = closedBy,
            ClosingAmount = closingAmount,
            Notes = notes ?? "Cierre de caja"
        });

        _logger.LogInformation("Caja cerrada: Id={Id}, Company={CompanyId}, ClosedBy={ClosedBy}, ClosingAmount={ClosingAmount}, Difference={Difference}",
            result.Id, result.CompanyId, result.ClosedBy, result.ClosingAmount, result.Difference);

        return result;
    }

    public async Task<CashMovement> AddMovementAsync(CashMovement movement)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.cash_movements (
                company_id, cash_register_id, type, amount, reason, notes, created_by, created_at
            ) VALUES (
                @CompanyId, @CashRegisterId, @Type, @Amount, @Reason, @Notes, @CreatedBy, NOW()
            )
            RETURNING id AS Id, company_id AS CompanyId, cash_register_id AS CashRegisterId,
                      type AS Type, amount AS Amount, reason AS Reason, notes AS Notes,
                      created_by AS CreatedBy, created_at AS CreatedAt";

        var result = await connection.QuerySingleAsync<CashMovement>(sql, new
        {
            movement.CompanyId,
            movement.CashRegisterId,
            movement.Type,
            movement.Amount,
            movement.Reason,
            movement.Notes,
            movement.CreatedBy
        });

        _logger.LogInformation("Movimiento de caja: Type={Type}, Amount={Amount}, Register={CashRegisterId}, User={CreatedBy}",
            result.Type, result.Amount, result.CashRegisterId, result.CreatedBy);

        return result;
    }

    public async Task<IEnumerable<CashMovement>> GetMovementsAsync(long cashRegisterId, long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                cm.id AS Id, cm.company_id AS CompanyId, cm.cash_register_id AS CashRegisterId,
                cm.type AS Type, cm.amount AS Amount, cm.reason AS Reason, cm.notes AS Notes,
                cm.created_by AS CreatedBy, cm.created_at AS CreatedAt,
                u.first_name || ' ' || u.last_name AS CreatedByName
            FROM sales.cash_movements cm
            LEFT JOIN core.users u ON cm.created_by = u.id
            WHERE cm.cash_register_id = @CashRegisterId AND cm.company_id = @CompanyId
            ORDER BY cm.created_at DESC";

        return await connection.QueryAsync<CashMovement>(sql, new { CashRegisterId = cashRegisterId, CompanyId = companyId });
    }

    public async Task UpdateTotalsAsync(long id, long companyId, decimal totalSales, decimal totalCashSales, decimal totalCardSales, decimal totalTransferSales, decimal totalOtherSales, decimal totalDiscounts, decimal totalCredits, decimal totalTips, int orderCount)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            UPDATE sales.cash_registers SET
                total_sales = total_sales + @TotalSales,
                total_cash_sales = total_cash_sales + @TotalCashSales,
                total_card_sales = total_card_sales + @TotalCardSales,
                total_transfer_sales = total_transfer_sales + @TotalTransferSales,
                total_other_sales = total_other_sales + @TotalOtherSales,
                total_discounts = total_discounts + @TotalDiscounts,
                total_credits = total_credits + @TotalCredits,
                total_tips = total_tips + @TotalTips,
                order_count = order_count + @OrderCount,
                updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            CompanyId = companyId,
            TotalSales = totalSales,
            TotalCashSales = totalCashSales,
            TotalCardSales = totalCardSales,
            TotalTransferSales = totalTransferSales,
            TotalOtherSales = totalOtherSales,
            TotalDiscounts = totalDiscounts,
            TotalCredits = totalCredits,
            TotalTips = totalTips,
            OrderCount = orderCount
        });
    }

    public async Task<IEnumerable<CashRegister>> GetHistoryAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                cr.id AS Id, cr.company_id AS CompanyId, cr.branch_id AS BranchId,
                cr.opened_by AS OpenedBy, cr.closed_by AS ClosedBy,
                cr.status AS Status, cr.opening_amount AS OpeningAmount,
                cr.closing_amount AS ClosingAmount, cr.expected_cash AS ExpectedCash,
                cr.difference AS Difference, cr.total_sales AS TotalSales,
                cr.total_cash_sales AS TotalCashSales, cr.total_card_sales AS TotalCardSales,
                cr.total_transfer_sales AS TotalTransferSales, cr.total_other_sales AS TotalOtherSales,
                cr.total_discounts AS TotalDiscounts, cr.total_credits AS TotalCredits,
                cr.total_tips AS TotalTips, cr.cash_in AS CashIn, cr.cash_out AS CashOut,
                cr.order_count AS OrderCount, cr.notes AS Notes,
                cr.opened_at AS OpenedAt, cr.closed_at AS ClosedAt,
                uo.first_name || ' ' || uo.last_name AS OpenedByName,
                uc.first_name || ' ' || uc.last_name AS ClosedByName
            FROM sales.cash_registers cr
            LEFT JOIN core.users uo ON cr.opened_by = uo.id
            LEFT JOIN core.users uc ON cr.closed_by = uc.id
            WHERE cr.company_id = @CompanyId 
              AND cr.branch_id = @BranchId
              AND cr.deleted_at IS NULL
              AND (@DateFrom::timestamp IS NULL OR cr.opened_at >= @DateFrom)
              AND (@DateTo::timestamp IS NULL OR cr.opened_at <= @DateTo)
            ORDER BY cr.opened_at DESC
            LIMIT @Limit OFFSET @Offset";

        return await connection.QueryAsync<CashRegister>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Limit = limit,
            Offset = (page - 1) * limit
        });
    }

    public async Task<int> GetHistoryCountAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT COUNT(*) 
            FROM sales.cash_registers
            WHERE company_id = @CompanyId 
              AND branch_id = @BranchId
              AND deleted_at IS NULL
              AND (@DateFrom::timestamp IS NULL OR opened_at >= @DateFrom)
              AND (@DateTo::timestamp IS NULL OR opened_at <= @DateTo)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo
        });
    }
}
