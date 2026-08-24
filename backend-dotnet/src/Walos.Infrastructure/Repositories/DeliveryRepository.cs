using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class DeliveryRepository : IDeliveryRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<DeliveryRepository> _logger;

    private static readonly HashSet<string> TimestampColumns = new(StringComparer.Ordinal)
    {
        "accepted_at", "prepared_at", "dispatched_at", "delivered_at"
    };

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
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", companyId);
        parameters.Add("BranchId", branchId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            where += " AND o.status = @Status";
            parameters.Add("Status", status);
        }

        if (dateFrom.HasValue)
        {
            where += " AND o.created_at >= @DateFrom";
            parameters.Add("DateFrom", dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            where += " AND o.created_at <= @DateTo";
            parameters.Add("DateTo", dateTo.Value);
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

        return await conn.QueryAsync<DeliveryOrder>(sql, parameters);
    }

    public async Task<DeliveryOrder?> GetOrderByIdAsync(long orderId, long companyId, long branchId)
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
            WHERE o.id = @OrderId
              AND o.company_id = @CompanyId
              AND o.branch_id = @BranchId
              AND o.deleted_at IS NULL";

        var args = new { OrderId = orderId, CompanyId = companyId, BranchId = branchId };
        var order = await conn.QueryFirstOrDefaultAsync<DeliveryOrder>(orderSql, args);
        if (order is null)
            return null;

        const string itemsSql = @"
            SELECT id AS Id, order_id AS OrderId, company_id AS CompanyId,
                   product_id AS ProductId, product_name AS ProductName,
                   quantity AS Quantity, unit_price AS UnitPrice, subtotal AS Subtotal,
                   notes AS Notes, created_at AS CreatedAt
            FROM delivery.order_items
            WHERE order_id = @OrderId AND company_id = @CompanyId
            ORDER BY id";

        order.Items = (await conn.QueryAsync<DeliveryOrderItem>(itemsSql, args)).ToList();
        order.StatusHistory = (await QueryStatusHistoryAsync(conn, orderId, companyId, branchId)).ToList();
        return order;
    }

    public async Task<DeliveryOrder> CreateOrderAsync(DeliveryOrder order, List<DeliveryOrderItem> requestedItems)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            const string branchSql = @"
                SELECT id
                FROM core.branches
                WHERE id = @BranchId
                  AND company_id = @CompanyId
                  AND is_active = TRUE
                  AND deleted_at IS NULL
                FOR SHARE";

            var branch = await conn.ExecuteScalarAsync<long?>(branchSql, new
            {
                order.CompanyId,
                order.BranchId
            }, tx);
            if (!branch.HasValue)
                throw new ValidationException("Sucursal no encontrada.");

            if (requestedItems.Count == 0)
                throw new ValidationException("El pedido debe tener al menos un item.");

            if (requestedItems.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
                throw new ValidationException("Todos los items deben tener producto y cantidad positiva.");

            if (order.DeliveryFee < 0 || order.DiscountAmount < 0)
                throw new ValidationException("El costo de domicilio y el descuento no pueden ser negativos.");

            var productIds = requestedItems.Select(item => item.ProductId).Distinct().ToArray();
            const string productsSql = @"
                SELECT id AS Id, name AS Name, sale_price AS SalePrice,
                       COALESCE(is_active, FALSE) AS IsActive,
                       COALESCE(is_for_sale, FALSE) AS IsForSale,
                       product_type AS ProductType
                FROM inventory.products
                WHERE company_id = @CompanyId
                  AND id = ANY(@ProductIds)
                  AND deleted_at IS NULL
                FOR SHARE";

            var products = (await conn.QueryAsync<DeliveryProductRow>(productsSql, new
            {
                order.CompanyId,
                ProductIds = productIds
            }, tx)).ToDictionary(product => product.Id);

            if (products.Count != productIds.Length)
                throw new ValidationException("Uno o mas productos no fueron encontrados.");

            if (products.Values.Any(product => !product.IsActive || !product.IsForSale))
                throw new ValidationException("Uno o mas productos no estan disponibles para venta.");

            if (products.Values.Any(product => product.SalePrice < 0))
                throw new ValidationException("Uno o mas productos tienen un precio de venta invalido.");

            var items = requestedItems.Select(requested =>
            {
                var product = products[requested.ProductId];
                return new DeliveryOrderItem
                {
                    CompanyId = order.CompanyId,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = requested.Quantity,
                    UnitPrice = Math.Round(product.SalePrice, 2, MidpointRounding.AwayFromZero),
                    Notes = requested.Notes
                };
            }).ToList();

            order.Subtotal = Math.Round(
                items.Sum(item => item.Quantity * item.UnitPrice),
                2,
                MidpointRounding.AwayFromZero);
            if (order.DiscountAmount > order.Subtotal + order.DeliveryFee)
                throw new ValidationException("El descuento no puede superar el valor del pedido.");

            order.Total = order.Subtotal + order.DeliveryFee - order.DiscountAmount;

            await conn.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0))",
                new { LockKey = $"delivery:{order.CompanyId}:{order.BranchId}" },
                tx);

            const string nextNumberSql = @"
                SELECT COALESCE(MAX(CAST(SUBSTRING(order_number FROM '[0-9]+$') AS INTEGER)), 0) + 1
                FROM delivery.orders
                WHERE company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND created_at >= date_trunc('day', NOW())";
            var sequence = await conn.ExecuteScalarAsync<int>(nextNumberSql, new
            {
                order.CompanyId,
                order.BranchId
            }, tx);
            order.OrderNumber = $"DEL-{sequence:D4}";

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
                INSERT INTO delivery.order_items (
                    company_id, order_id, product_id, product_name, quantity, unit_price, notes
                ) VALUES (
                    @CompanyId, @OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, @Notes
                )
                RETURNING id AS Id, subtotal AS Subtotal, created_at AS CreatedAt";

            foreach (var item in items)
            {
                item.OrderId = created.Id;
                var inserted = await conn.QuerySingleAsync<DeliveryItemInsertResult>(itemSql, item, tx);
                item.Id = inserted.Id;
                item.Subtotal = inserted.Subtotal;
                item.CreatedAt = inserted.CreatedAt;
            }

            const string historySql = @"
                INSERT INTO delivery.status_history (order_id, from_status, to_status, comment, changed_by)
                VALUES (@OrderId, NULL, 'new', 'Pedido creado', @ChangedBy)";
            await conn.ExecuteAsync(historySql, new
            {
                OrderId = created.Id,
                ChangedBy = order.CreatedBy
            }, tx);

            tx.Commit();
            created.Items = items;
            _logger.LogInformation("Pedido delivery {OrderId} creado para company {CompanyId} branch {BranchId}",
                created.Id, order.CompanyId, order.BranchId);
            return created;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(
        long orderId,
        long companyId,
        long branchId,
        string expectedStatus,
        string newStatus,
        string? comment,
        long? changedBy,
        Dictionary<string, DateTime?> timestamps)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            const string currentSql = @"
                SELECT status
                FROM delivery.orders
                WHERE id = @Id
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND deleted_at IS NULL
                FOR UPDATE";
            var args = new { Id = orderId, CompanyId = companyId, BranchId = branchId };
            var currentStatus = await conn.ExecuteScalarAsync<string?>(currentSql, args, tx);
            if (currentStatus is null || !string.Equals(currentStatus, expectedStatus, StringComparison.Ordinal))
            {
                tx.Rollback();
                return false;
            }

            var setClauses = new List<string> { "status = @NewStatus", "updated_at = NOW()" };
            var parameters = new DynamicParameters();
            parameters.Add("NewStatus", newStatus);
            parameters.Add("ExpectedStatus", expectedStatus);
            parameters.Add("Id", orderId);
            parameters.Add("CompanyId", companyId);
            parameters.Add("BranchId", branchId);

            foreach (var timestamp in timestamps.Where(entry => entry.Value.HasValue))
            {
                if (!TimestampColumns.Contains(timestamp.Key))
                    throw new ValidationException("Timestamp de estado no permitido.");

                setClauses.Add($"{timestamp.Key} = @{timestamp.Key}");
                parameters.Add(timestamp.Key, timestamp.Value);
            }

            if (newStatus == "rejected" && timestamps.ContainsKey("rejected_reason"))
            {
                setClauses.Add("rejected_reason = @RejectedReason");
                parameters.Add("RejectedReason", comment);
            }
            else if (newStatus == "returned" && timestamps.ContainsKey("returned_reason"))
            {
                setClauses.Add("returned_reason = @ReturnedReason");
                parameters.Add("ReturnedReason", comment);
            }

            var updateSql = $@"
                UPDATE delivery.orders
                SET {string.Join(", ", setClauses)}
                WHERE id = @Id
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND status = @ExpectedStatus
                  AND deleted_at IS NULL";
            var affected = await conn.ExecuteAsync(updateSql, parameters, tx);
            if (affected != 1)
            {
                tx.Rollback();
                return false;
            }

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

    public async Task<IEnumerable<DeliveryStatusHistory>> GetStatusHistoryAsync(
        long orderId,
        long companyId,
        long branchId)
    {
        using var conn = await _db.CreateConnectionAsync();
        return await QueryStatusHistoryAsync(conn, orderId, companyId, branchId);
    }

    private static Task<IEnumerable<DeliveryStatusHistory>> QueryStatusHistoryAsync(
        IDbConnection conn,
        long orderId,
        long companyId,
        long branchId)
    {
        const string sql = @"
            SELECT h.id AS Id, h.order_id AS OrderId, h.from_status AS FromStatus,
                   h.to_status AS ToStatus, h.comment AS Comment,
                   h.changed_by AS ChangedBy, h.created_at AS CreatedAt
            FROM delivery.status_history h
            INNER JOIN delivery.orders o ON o.id = h.order_id
            WHERE h.order_id = @OrderId
              AND o.company_id = @CompanyId
              AND o.branch_id = @BranchId
              AND o.deleted_at IS NULL
            ORDER BY h.created_at ASC, h.id ASC";
        return conn.QueryAsync<DeliveryStatusHistory>(sql, new
        {
            OrderId = orderId,
            CompanyId = companyId,
            BranchId = branchId
        });
    }

    private sealed class DeliveryProductRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal SalePrice { get; init; }
        public bool IsActive { get; init; }
        public bool IsForSale { get; init; }
        public string ProductType { get; init; } = string.Empty;
    }

    private sealed class DeliveryItemInsertResult
    {
        public long Id { get; init; }
        public decimal Subtotal { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
