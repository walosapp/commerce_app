using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class RefundRepository : IRefundRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<RefundRepository> _logger;

    public RefundRepository(IDbConnectionFactory connectionFactory, ILogger<RefundRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Refund> CreateAsync(Refund refund)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.refunds (
                company_id, branch_id, order_id, refund_type, refund_amount,
                reason, status, approved_by, created_by, created_at
            ) VALUES (
                @CompanyId, @BranchId, @OrderId, @RefundType, @RefundAmount,
                @Reason, @Status, @ApprovedBy, @CreatedBy, NOW()
            ) RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                        order_id AS OrderId, refund_type AS RefundType, refund_amount AS RefundAmount,
                        reason AS Reason, status AS Status, approved_by AS ApprovedBy,
                        created_by AS CreatedBy, created_at AS CreatedAt";

        return await connection.QuerySingleAsync<Refund>(sql, refund);
    }

    public async Task CreateItemsAsync(long refundId, List<RefundItem> items)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.refund_items (
                refund_id, order_item_id, quantity, unit_price, subtotal, created_at
            ) VALUES (
                @RefundId, @OrderItemId, @Quantity, @UnitPrice, @Subtotal, NOW()
            )";

        foreach (var item in items)
        {
            item.RefundId = refundId;
            await connection.ExecuteAsync(sql, item);
        }
    }

    public async Task<Refund?> GetByIdAsync(long id, long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType, r.refund_amount AS RefundAmount,
                   r.reason AS Reason, r.status AS Status, r.approved_by AS ApprovedBy,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt
            FROM sales.refunds r
            WHERE r.id = @Id AND r.company_id = @CompanyId";

        var refund = await connection.QuerySingleOrDefaultAsync<Refund>(sql, new { Id = id, CompanyId = companyId });
        if (refund != null)
        {
            const string itemsSql = @"
                SELECT ri.id AS Id, ri.refund_id AS RefundId, ri.order_item_id AS OrderItemId,
                       ri.quantity AS Quantity, ri.unit_price AS UnitPrice, ri.subtotal AS Subtotal,
                       ri.created_at AS CreatedAt
                FROM sales.refund_items ri
                WHERE ri.refund_id = @RefundId";

            refund.Items = (await connection.QueryAsync<RefundItem>(itemsSql, new { RefundId = refund.Id })).ToList();
        }
        return refund;
    }

    public async Task<IEnumerable<Refund>> GetByOrderIdAsync(long orderId, long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType, r.refund_amount AS RefundAmount,
                   r.reason AS Reason, r.status AS Status, r.approved_by AS ApprovedBy,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt
            FROM sales.refunds r
            WHERE r.order_id = @OrderId AND r.company_id = @CompanyId
            ORDER BY r.created_at DESC";

        return await connection.QueryAsync<Refund>(sql, new { OrderId = orderId, CompanyId = companyId });
    }

    public async Task<IEnumerable<Refund>> GetAllAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var sql = @"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType, r.refund_amount AS RefundAmount,
                   r.reason AS Reason, r.status AS Status,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt,
                   o.order_number AS OrderNumber,
                   u.first_name || ' ' || u.last_name AS CreatedByName
            FROM sales.refunds r
            JOIN sales.orders o ON o.id = r.order_id
            LEFT JOIN core.users u ON u.id = r.created_by
            WHERE r.company_id = @CompanyId AND r.branch_id = @BranchId";

        if (dateFrom.HasValue) sql += " AND r.created_at >= @DateFrom";
        if (dateTo.HasValue) sql += " AND r.created_at <= @DateTo";
        sql += " ORDER BY r.created_at DESC LIMIT @Limit OFFSET @Offset";

        return await connection.QueryAsync<Refund>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo?.AddDays(1),
            Limit = limit,
            Offset = (page - 1) * limit
        });
    }

    public async Task<int> GetCountAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var sql = @"
            SELECT COUNT(*) FROM sales.refunds
            WHERE company_id = @CompanyId AND branch_id = @BranchId";

        if (dateFrom.HasValue) sql += " AND created_at >= @DateFrom";
        if (dateTo.HasValue) sql += " AND created_at <= @DateTo";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo?.AddDays(1)
        });
    }

    public async Task UpdateOrderRefundStatusAsync(long orderId, long companyId, string refundStatus)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            UPDATE sales.orders
            SET refund_status = @RefundStatus, updated_at = NOW()
            WHERE id = @OrderId AND company_id = @CompanyId";

        await connection.ExecuteAsync(sql, new { OrderId = orderId, CompanyId = companyId, RefundStatus = refundStatus });
    }
}
