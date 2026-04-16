using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Delivery;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class DeliveryRepository : IDeliveryRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<DeliveryRepository> _logger;

    public DeliveryRepository(IDbConnectionFactory db, ILogger<DeliveryRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<DeliveryOrder>> GetOrdersAsync(
        long companyId, long branchId, string? status, DateTime? dateFrom, DateTime? dateTo)
    {
        using var conn = await _db.CreateConnectionAsync();

        var where = "WHERE o.company_id = @CompanyId AND o.branch_id = @BranchId AND o.deleted_at IS NULL";
        var p = new DynamicParameters();
        p.Add("CompanyId", companyId);
        p.Add("BranchId", branchId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            where += " AND o.status = @Status";
            p.Add("Status", status);
        }
        if (dateFrom.HasValue)
        {
            where += " AND o.created_at >= @DateFrom";
            p.Add("DateFrom", dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            where += " AND o.created_at <= @DateTo";
            p.Add("DateTo", dateTo.Value);
        }

        var sql = $@"
            SELECT
                o.id AS Id, o.company_id AS CompanyId, o.branch_id AS BranchId,
                o.source AS Source, o.external_order_id AS ExternalOrderId,
                o.order_number AS OrderNumber, o.status AS Status,
                o.customer_name AS CustomerName, o.customer_phone AS CustomerPhone,
                o.customer_address AS CustomerAddress, o.notes AS Notes,
                o.subtotal AS Subtotal, o.delivery_fee AS DeliveryFee,
                o.discount_amount AS DiscountAmount, o.total AS Total,
                o.accepted_at AS AcceptedAt, o.prepared_at AS PreparedAt,
                o.dispatched_at AS DispatchedAt, o.delivered_at AS DeliveredAt,
                o.rejected_reason AS RejectedReason, o.returned_reason AS ReturnedReason,
                o.created_by AS CreatedBy, o.created_at AS CreatedAt, o.updated_at AS UpdatedAt
            FROM delivery.orders o
            {where}
            ORDER BY o.created_at DESC";

        return await conn.QueryAsync<DeliveryOrder>(sql, p);
    }

    public async Task<DeliveryOrder?> GetOrderByIdAsync(long orderId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();

        const string orderSql = @"
            SELECT
                o.id AS Id, o.company_id AS CompanyId, o.branch_id AS BranchId,
                o.source AS Source, o.external_order_id AS ExternalOrderId,
                o.order_number AS OrderNumber, o.status AS Status,
                o.customer_name AS CustomerName, o.customer_phone AS CustomerPhone,
                o.customer_address AS CustomerAddress, o.notes AS Notes,
                o.subtotal AS Subtotal, o.delivery_fee AS DeliveryFee,
                o.discount_amount AS DiscountAmount, o.total AS Total,
                o.accepted_at AS AcceptedAt, o.prepared_at AS PreparedAt,
                o.dispatched_at AS DispatchedAt, o.delivered_at AS DeliveredAt,
                o.rejected_reason AS RejectedReason, o.returned_reason AS ReturnedReason,
                o.created_by AS CreatedBy, o.created_at AS CreatedAt, o.updated_at AS UpdatedAt
            FROM delivery.orders o
            WHERE o.id = @OrderId AND o.company_id = @CompanyId AND o.deleted_at IS NULL";

        var order = await conn.QueryFirstOrDefaultAsync<DeliveryOrder>(orderSql, new { OrderId = orderId, CompanyId = companyId });
        if (order is null) return null;

        const string itemsSql = @"
            SELECT id AS Id, order_id AS OrderId, company_id AS CompanyId,
                   product_id AS ProductId, product_name AS ProductName,
                   quantity AS Quantity, unit_price AS UnitPrice, subtotal AS Subtotal,
                   notes AS Notes, created_at AS CreatedAt
            FROM delivery.order_items
            WHERE order_id = @OrderId AND company_id = @CompanyId";

        order.Items = (await conn.QueryAsync<DeliveryOrderItem>(itemsSql, new { OrderId = orderId, CompanyId = companyId })).ToList();
        order.StatusHistory = (await GetStatusHistoryAsync(orderId)).ToList();

        return order;
    }

    public async Task<DeliveryOrder> CreateOrderAsync(DeliveryOrder order, List<DeliveryOrderItem> items)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            const string orderSql = @"
                INSERT INTO delivery.orders (
                    company_id, branch_id, source, order_number, status,
                    customer_name, customer_phone, customer_address, notes,
                    subtotal, delivery_fee, discount_amount, total, created_by
                ) VALUES (
                    @CompanyId, @BranchId, @Source, @OrderNumber, 'new',
                    @CustomerName, @CustomerPhone, @CustomerAddress, @Notes,
                    @Subtotal, @DeliveryFee, @DiscountAmount, @Total, @CreatedBy
                ) RETURNING id AS Id, order_number AS OrderNumber, status AS Status,
                             created_at AS CreatedAt, company_id AS CompanyId, branch_id AS BranchId,
                             source AS Source, customer_name AS CustomerName, customer_phone AS CustomerPhone,
                             customer_address AS CustomerAddress, notes AS Notes,
                             subtotal AS Subtotal, delivery_fee AS DeliveryFee,
                             discount_amount AS DiscountAmount, total AS Total, created_by AS CreatedBy";

            var created = await conn.QuerySingleAsync<DeliveryOrder>(orderSql, order, tx);

            const string itemSql = @"
                INSERT INTO delivery.order_items (company_id, order_id, product_id, product_name, quantity, unit_price, notes)
                VALUES (@CompanyId, @OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, @Notes)";

            foreach (var item in items)
            {
                item.OrderId = created.Id;
                item.CompanyId = order.CompanyId;
                await conn.ExecuteAsync(itemSql, item, tx);
            }

            const string historySql = @"
                INSERT INTO delivery.status_history (order_id, from_status, to_status, comment, changed_by)
                VALUES (@OrderId, NULL, 'new', 'Pedido creado', @ChangedBy)";
            await conn.ExecuteAsync(historySql, new { OrderId = created.Id, ChangedBy = order.CreatedBy }, tx);

            tx.Commit();

            created.Items = items;
            return created;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(
        long orderId, long companyId, string newStatus, string? comment, long? changedBy,
        Dictionary<string, DateTime?> timestamps)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var currentSql = "SELECT status FROM delivery.orders WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL";
            var currentStatus = await conn.ExecuteScalarAsync<string>(currentSql, new { Id = orderId, CompanyId = companyId }, tx);
            if (currentStatus is null) return false;

            var setClauses = new List<string> { "status = @NewStatus", "updated_at = NOW()" };
            var p = new DynamicParameters();
            p.Add("NewStatus", newStatus);
            p.Add("Id", orderId);
            p.Add("CompanyId", companyId);

            foreach (var kv in timestamps.Where(kv => kv.Value.HasValue))
            {
                setClauses.Add($"{kv.Key} = @{kv.Key}");
                p.Add(kv.Key, kv.Value);
            }

            if (newStatus == "rejected" && timestamps.ContainsKey("rejected_reason"))
            {
                setClauses.Add("rejected_reason = @RejectedReason");
                p.Add("RejectedReason", comment);
            }
            else if (newStatus == "returned" && timestamps.ContainsKey("returned_reason"))
            {
                setClauses.Add("returned_reason = @ReturnedReason");
                p.Add("ReturnedReason", comment);
            }

            var updateSql = $"UPDATE delivery.orders SET {string.Join(", ", setClauses)} WHERE id = @Id AND company_id = @CompanyId";
            await conn.ExecuteAsync(updateSql, p, tx);

            const string historySql = @"
                INSERT INTO delivery.status_history (order_id, from_status, to_status, comment, changed_by)
                VALUES (@OrderId, @FromStatus, @ToStatus, @Comment, @ChangedBy)";
            await conn.ExecuteAsync(historySql, new
            {
                OrderId = orderId,
                FromStatus = currentStatus,
                ToStatus = newStatus,
                Comment = comment,
                ChangedBy = changedBy
            }, tx);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<DeliveryStatusHistory>> GetStatusHistoryAsync(long orderId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT id AS Id, order_id AS OrderId, from_status AS FromStatus,
                   to_status AS ToStatus, comment AS Comment,
                   changed_by AS ChangedBy, created_at AS CreatedAt
            FROM delivery.status_history
            WHERE order_id = @OrderId
            ORDER BY created_at ASC";
        return await conn.QueryAsync<DeliveryStatusHistory>(sql, new { OrderId = orderId });
    }

    public async Task<string> GetNextOrderNumberAsync(long companyId, long branchId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT COALESCE(MAX(CAST(SUBSTRING(order_number FROM '[0-9]+$') AS INTEGER)), 0) + 1
            FROM delivery.orders
            WHERE company_id = @CompanyId AND branch_id = @BranchId
              AND created_at >= date_trunc('day', NOW())";
        var seq = await conn.ExecuteScalarAsync<int>(sql, new { CompanyId = companyId, BranchId = branchId });
        return $"DEL-{seq:D4}";
    }
}
