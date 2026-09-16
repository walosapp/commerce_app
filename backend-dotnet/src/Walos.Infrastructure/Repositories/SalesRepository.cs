using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class SalesRepository : ISalesRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SalesRepository> _logger;

    private sealed class PendingOrderSummary
    {
        public long Id { get; set; }
        public long TableId { get; set; }
        public decimal Total { get; set; }
    }

    public SalesRepository(IDbConnectionFactory connectionFactory, ILogger<SalesRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string tableSql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       table_number AS TableNumber, name AS Name, status AS Status,
                       created_by AS CreatedBy, created_at AS CreatedAt
                FROM sales.tables
                WHERE company_id = @CompanyId AND branch_id = @BranchId
                  AND status = 'open' AND deleted_at IS NULL
                ORDER BY table_number";

            var tables = (await connection.QueryAsync<SalesTable>(tableSql, new { CompanyId = companyId, BranchId = branchId })).ToList();

            if (tables.Count == 0) return tables;

            var tableIds = tables.Select(t => t.Id).ToArray();

            const string itemsSql = @"
                SELECT oi.id AS Id, oi.order_id AS OrderId, oi.product_id AS ProductId,
                       oi.product_name AS ProductName, oi.quantity AS Quantity,
                       oi.unit_price AS UnitPrice, oi.subtotal AS Subtotal,
                       p.image_url AS ImageUrl
                FROM sales.order_items oi
                INNER JOIN sales.orders o ON oi.order_id = o.id AND o.company_id = oi.company_id
                LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id
                WHERE o.table_id = ANY(@TableIds) AND o.company_id = @CompanyId AND o.status = 'pending'";

            var allItems = (await connection.QueryAsync<OrderItem>(itemsSql, new { TableIds = tableIds, CompanyId = companyId })).ToList();

            const string ordersSql = @"
                SELECT id AS Id, table_id AS TableId, total AS Total
                FROM sales.orders
                WHERE table_id = ANY(@TableIds) AND company_id = @CompanyId AND status = 'pending'";

            var orders = (await connection.QueryAsync<PendingOrderSummary>(ordersSql, new { TableIds = tableIds, CompanyId = companyId })).ToList();

            foreach (var table in tables)
            {
                var order = orders.FirstOrDefault(o => o.TableId == table.Id);
                if (order != null)
                {
                    var orderId = order.Id;
                    table.Items = allItems.Where(i => i.OrderId == orderId).ToList();
                    table.Total = order.Total;
                }
                else
                {
                    table.Items = new List<OrderItem>();
                    table.Total = 0;
                }
            }

            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo mesas activas");
            throw;
        }
    }

    public Task<SalesTable?> GetTableByIdAsync(long tableId, long companyId)
        => GetTableByIdAsync(tableId, companyId, null);

    public async Task<SalesTable?> GetTableByIdAsync(long tableId, long companyId, long? branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       table_number AS TableNumber, name AS Name, status AS Status,
                       created_by AS CreatedBy, created_at AS CreatedAt
                FROM sales.tables
                WHERE id = @TableId AND company_id = @CompanyId
                  AND (@BranchId IS NULL OR branch_id = @BranchId)
                  AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<SalesTable>(sql,
                new { TableId = tableId, CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo mesa {TableId}", tableId);
            throw;
        }
    }

    public async Task<SalesTable> CreateTableAsync(SalesTable table)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO sales.tables (company_id, branch_id, table_number, name, status, created_by, created_at)
                VALUES (@CompanyId, @BranchId, @TableNumber, @Name, @Status, @CreatedBy, NOW())
                RETURNING id";

            table.Id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                table.CompanyId,
                table.BranchId,
                table.TableNumber,
                table.Name,
                table.Status,
                table.CreatedBy
            });

            return table;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando mesa");
            throw;
        }
    }

    public async Task<Order> CreateOrderAsync(Order order, List<OrderItem> items)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string orderSql = @"
                    INSERT INTO sales.orders (company_id, branch_id, table_id, order_number, status, subtotal, tax, total, notes, created_by, created_at)
                    VALUES (@CompanyId, @BranchId, @TableId, @OrderNumber, @Status, @Subtotal, @Tax, @Total, @Notes, @CreatedBy, NOW())
                    RETURNING id";

                order.Id = await connection.ExecuteScalarAsync<long>(orderSql, new
                {
                    order.CompanyId,
                    order.BranchId,
                    order.TableId,
                    order.OrderNumber,
                    order.Status,
                    order.Subtotal,
                    order.Tax,
                    order.Total,
                    order.Notes,
                    order.CreatedBy
                }, transaction);

                const string itemSql = @"
                    INSERT INTO sales.order_items (company_id, order_id, product_id, product_name, quantity, unit_price, created_at)
                    VALUES (@CompanyId, @OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, NOW())";

                foreach (var item in items)
                {
                    item.OrderId = order.Id;
                    await connection.ExecuteAsync(itemSql, new
                    {
                        CompanyId = order.CompanyId,
                        item.OrderId,
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        item.UnitPrice
                    }, transaction);
                }

                transaction.Commit();
                order.Items = items;
                return order;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando orden para mesa {TableId}", order.TableId);
            throw;
        }
    }

    public Task<Order?> GetOrderByTableIdAsync(long tableId, long companyId)
        => GetOrderByTableIdAsync(tableId, companyId, null);

    public async Task<Order?> GetOrderByTableIdAsync(long tableId, long companyId, long? branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                 SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                        table_id AS TableId, cash_register_id AS CashRegisterId, order_number AS OrderNumber, status AS Status,
                        subtotal AS Subtotal, tax AS Tax, total AS Total, notes AS Notes,
                        created_by AS CreatedBy, created_at AS CreatedAt
                 FROM sales.orders
                WHERE table_id = @TableId AND company_id = @CompanyId
                  AND (@BranchId IS NULL OR branch_id = @BranchId)
                  AND status = 'pending'
                ORDER BY created_at DESC";

            return await connection.QueryFirstOrDefaultAsync<Order>(sql,
                new { TableId = tableId, CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo orden de mesa {TableId}", tableId);
            throw;
        }
    }

    public Task<Order?> GetOrderByIdAsync(long orderId, long companyId)
        => GetOrderByIdAsync(orderId, companyId, null);

    public async Task<Order?> GetOrderByIdAsync(long orderId, long companyId, long? branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                 SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                        table_id AS TableId, cash_register_id AS CashRegisterId, order_number AS OrderNumber, status AS Status,
                        subtotal AS Subtotal, tax AS Tax, total AS Total, notes AS Notes,
                        discount_type AS DiscountType, discount_value AS DiscountValue,
                        discount_amount AS DiscountAmount, final_total_paid AS FinalTotalPaid,
                        split_reference_count AS SplitReferenceCount,
                        payment_method AS PaymentMethod,
                        tip_amount AS TipAmount, tip_included AS TipIncluded,
                        refund_status AS RefundStatus,
                        created_by AS CreatedBy, created_at AS CreatedAt
                FROM sales.orders
                WHERE id = @OrderId AND company_id = @CompanyId
                  AND (@BranchId IS NULL OR branch_id = @BranchId)
                  AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<Order>(sql,
                new { OrderId = orderId, CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo orden {OrderId}", orderId);
            throw;
        }
    }

    public Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId, long companyId)
        => GetOrderItemsAsync(orderId, companyId, null);

    public async Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId, long companyId, long? branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT oi.id AS Id, oi.order_id AS OrderId, oi.product_id AS ProductId,
                       oi.product_name AS ProductName, oi.quantity AS Quantity,
                       oi.unit_price AS UnitPrice, oi.subtotal AS Subtotal,
                       p.image_url AS ImageUrl
                FROM sales.order_items oi
                INNER JOIN sales.orders o ON o.id = oi.order_id AND o.company_id = oi.company_id
                LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id
                WHERE oi.order_id = @OrderId AND oi.company_id = @CompanyId
                  AND (@BranchId IS NULL OR o.branch_id = @BranchId)
                ORDER BY oi.id ASC";

            return await connection.QueryAsync<OrderItem>(sql,
                new { OrderId = orderId, CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo items de orden {OrderId}", orderId);
            throw;
        }
    }

    public Task<OrderItem?> GetOrderItemByIdAsync(long itemId, long companyId)
        => GetOrderItemByIdAsync(itemId, companyId, null);

    public async Task<OrderItem?> GetOrderItemByIdAsync(long itemId, long companyId, long? branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT oi.id AS Id, oi.order_id AS OrderId, oi.product_id AS ProductId,
                       oi.product_name AS ProductName, oi.quantity AS Quantity,
                       oi.unit_price AS UnitPrice, oi.subtotal AS Subtotal,
                       p.image_url AS ImageUrl
                FROM sales.order_items oi
                INNER JOIN sales.orders o ON o.id = oi.order_id AND o.company_id = oi.company_id
                LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id
                WHERE oi.id = @ItemId AND oi.company_id = @CompanyId
                  AND (@BranchId IS NULL OR o.branch_id = @BranchId)";

            return await connection.QueryFirstOrDefaultAsync<OrderItem>(sql,
                new { ItemId = itemId, CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo item {ItemId}", itemId);
            throw;
        }
    }

    public Task UpdateTableStatusAsync(long tableId, long companyId, string status)
        => UpdateTableStatusAsync(tableId, companyId, null, status);

    public async Task UpdateTableStatusAsync(long tableId, long companyId, long? branchId, string status)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE sales.tables
                SET status = @Status, updated_at = NOW()
                WHERE id = @TableId AND company_id = @CompanyId
                  AND (@BranchId IS NULL OR branch_id = @BranchId)";

            await connection.ExecuteAsync(sql,
                new { TableId = tableId, CompanyId = companyId, BranchId = branchId, Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado de mesa {TableId}", tableId);
            throw;
        }
    }

    public Task UpdateOrderStatusAsync(long orderId, long companyId, string status)
        => UpdateOrderStatusAsync(orderId, companyId, null, status);

    public async Task UpdateOrderStatusAsync(long orderId, long companyId, long? branchId, string status)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE sales.orders
                SET status = @Status, updated_at = NOW()
                WHERE id = @OrderId AND company_id = @CompanyId
                  AND (@BranchId IS NULL OR branch_id = @BranchId)";

            await connection.ExecuteAsync(sql,
                new { OrderId = orderId, CompanyId = companyId, BranchId = branchId, Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado de orden {OrderId}", orderId);
            throw;
        }
    }

    public async Task<bool> CancelActiveTableAsync(long tableId, long companyId, long? branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                WITH active_restaurant_sale AS MATERIALIZED (
                    SELECT t.id AS table_id, o.id AS order_id
                    FROM sales.tables t
                    INNER JOIN sales.orders o
                        ON o.table_id = t.id
                       AND o.company_id = t.company_id
                       AND o.branch_id = t.branch_id
                    WHERE t.id = @TableId
                      AND t.company_id = @CompanyId
                      AND (@BranchId IS NULL OR t.branch_id = @BranchId)
                      AND t.status = 'open'
                      AND t.deleted_at IS NULL
                      AND o.status = 'pending'
                      AND o.deleted_at IS NULL
                    FOR UPDATE OF t, o
                ), cancelled_orders AS (
                    UPDATE sales.orders o
                    SET status = 'cancelled', updated_at = NOW()
                    FROM active_restaurant_sale active
                    WHERE o.id = active.order_id
                      AND o.company_id = @CompanyId
                      AND o.status = 'pending'
                    RETURNING active.table_id
                ), cancelled_table AS (
                    UPDATE sales.tables t
                    SET status = 'cancelled', updated_at = NOW()
                    WHERE t.id IN (SELECT table_id FROM cancelled_orders)
                      AND t.company_id = @CompanyId
                      AND (@BranchId IS NULL OR t.branch_id = @BranchId)
                      AND t.status = 'open'
                    RETURNING t.id
                )
                SELECT EXISTS (SELECT 1 FROM cancelled_table);";

            return await connection.ExecuteScalarAsync<bool>(sql,
                new { TableId = tableId, CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando venta activa de mesa {TableId}", tableId);
            throw;
        }
    }

    public async Task<int> GetNextTableNumberAsync(long companyId, long branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT COALESCE(MAX(table_number), 0) + 1
                FROM sales.tables
                WHERE company_id = @CompanyId AND branch_id = @BranchId
                  AND status = 'open' AND deleted_at IS NULL";

            return await connection.ExecuteScalarAsync<int>(sql, new { CompanyId = companyId, BranchId = branchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo número de mesa siguiente");
            throw;
        }
    }

    public async Task UpdateOrderItemQuantityAsync(long orderItemId, long companyId, decimal quantity)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"UPDATE sales.order_items SET quantity = @Quantity WHERE id = @Id AND company_id = @CompanyId";
            await connection.ExecuteAsync(sql, new { Id = orderItemId, CompanyId = companyId, Quantity = quantity });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando cantidad de item {ItemId}", orderItemId);
            throw;
        }
    }

    public async Task DeleteOrderItemAsync(long orderItemId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"DELETE FROM sales.order_items WHERE id = @Id AND company_id = @CompanyId";
            await connection.ExecuteAsync(sql, new { Id = orderItemId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando item {ItemId}", orderItemId);
            throw;
        }
    }

    public async Task AddOrderItemAsync(OrderItem item)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO sales.order_items (company_id, order_id, product_id, product_name, quantity, unit_price, created_at)
                VALUES (@CompanyId, @OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, NOW())
                RETURNING id";
            item.Id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                item.CompanyId,
                item.OrderId,
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando item a orden {OrderId}", item.OrderId);
            throw;
        }
    }

    public async Task RecalculateOrderTotalAsync(long orderId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE sales.orders
                SET subtotal = COALESCE((SELECT SUM(subtotal) FROM sales.order_items WHERE order_id = @OrderId AND company_id = @CompanyId), 0),
                    total = COALESCE((SELECT SUM(subtotal) FROM sales.order_items WHERE order_id = @OrderId AND company_id = @CompanyId), 0),
                    updated_at = NOW()
                WHERE id = @OrderId AND company_id = @CompanyId";
            await connection.ExecuteAsync(sql, new { OrderId = orderId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculando total de orden {OrderId}", orderId);
            throw;
        }
    }
    public Task RenameTableAsync(long tableId, long companyId, string name)
        => RenameTableAsync(tableId, companyId, null, name);

    public async Task RenameTableAsync(long tableId, long companyId, long? branchId, string name)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE sales.tables
                SET name = @Name, updated_at = NOW()
                WHERE id = @TableId AND company_id = @CompanyId
                  AND (@BranchId IS NULL OR branch_id = @BranchId)
                  AND deleted_at IS NULL";
            await connection.ExecuteAsync(sql,
                new { TableId = tableId, CompanyId = companyId, BranchId = branchId, Name = name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renombrando mesa {TableId}", tableId);
            throw;
        }
    }

    public async Task UpdateOrderInvoiceSummaryAsync(long orderId, long companyId, string? discountType, decimal discountValue, decimal discountAmount, decimal finalTotalPaid, int splitReferenceCount, long? cashRegisterId, string? paymentMethod, decimal tipAmount, bool tipIncluded)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE sales.orders
                SET discount_type = @DiscountType,
                    discount_value = @DiscountValue,
                    discount_amount = @DiscountAmount,
                    final_total_paid = @FinalTotalPaid,
                    split_reference_count = @SplitReferenceCount,
                    cash_register_id = @CashRegisterId,
                    payment_method = @PaymentMethod,
                    tip_amount = @TipAmount,
                    tip_included = @TipIncluded,
                    total = @FinalTotalPaid,
                    updated_at = NOW()
                WHERE id = @OrderId AND company_id = @CompanyId";

            await connection.ExecuteAsync(sql, new
            {
                OrderId = orderId,
                CompanyId = companyId,
                DiscountType = discountType,
                DiscountValue = discountValue,
                DiscountAmount = discountAmount,
                FinalTotalPaid = finalTotalPaid,
                SplitReferenceCount = splitReferenceCount,
                CashRegisterId = cashRegisterId,
                PaymentMethod = paymentMethod,
                TipAmount = tipAmount,
                TipIncluded = tipIncluded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando resumen de factura {OrderId}", orderId);
            throw;
        }
    }

    public async Task<SalesSummary> GetSalesSummaryAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string summarySql = @"
                SELECT
                    COALESCE(SUM(o.final_total_paid), 0)   AS TotalRevenue,
                    COALESCE(SUM(o.discount_amount), 0)    AS TotalDiscounts,
                    COUNT(*)                                AS TotalOrders,
                    COALESCE(AVG(o.final_total_paid), 0)   AS AverageTicket,
                    COALESCE(SUM(CASE WHEN c.id IS NOT NULL THEN c.credit_amount ELSE 0 END), 0) AS TotalCredits,
                    COUNT(CASE WHEN c.id IS NOT NULL THEN 1 END) AS CreditOrders
                FROM sales.orders o
                LEFT JOIN sales.credits c ON c.order_id = o.id AND c.company_id = o.company_id
                WHERE o.company_id = @CompanyId
                  AND o.branch_id  = @BranchId
                  AND o.status     = 'completed'
                  AND COALESCE(o.refund_status, '') <> 'full_refund'
                  AND o.created_at >= @DateFrom
                  AND o.created_at <  @DateTo
                  AND o.deleted_at IS NULL";

            var summary = await connection.QueryFirstAsync<SalesSummary>(summarySql,
                new { CompanyId = companyId, BranchId = branchId, DateFrom = dateFrom, DateTo = dateTo });

            const string topSql = @"
                SELECT oi.product_name AS ProductName,
                       SUM(oi.quantity)             AS TotalQuantity,
                       SUM(oi.quantity * oi.unit_price) AS TotalRevenue
                FROM sales.order_items oi
                JOIN sales.orders o ON o.id = oi.order_id
                WHERE oi.company_id = @CompanyId
                  AND o.branch_id   = @BranchId
                  AND o.status      = 'completed'
                  AND COALESCE(o.refund_status, '') <> 'full_refund'
                  AND o.created_at >= @DateFrom
                  AND o.created_at <  @DateTo
                  AND o.deleted_at IS NULL
                GROUP BY oi.product_name
                ORDER BY TotalRevenue DESC
                LIMIT 5";

            summary.TopProducts = (await connection.QueryAsync<TopProduct>(topSql,
                new { CompanyId = companyId, BranchId = branchId, DateFrom = dateFrom, DateTo = dateTo })).ToList();

            const string hourlySql = @"
                SELECT EXTRACT(HOUR FROM o.created_at)::INT AS Hour,
                       COUNT(*)                              AS OrderCount,
                       COALESCE(SUM(o.final_total_paid), 0) AS Revenue
                FROM sales.orders o
                WHERE o.company_id = @CompanyId
                  AND o.branch_id  = @BranchId
                  AND o.status     = 'completed'
                  AND COALESCE(o.refund_status, '') <> 'full_refund'
                  AND o.created_at >= @DateFrom
                  AND o.created_at <  @DateTo
                  AND o.deleted_at IS NULL
                GROUP BY EXTRACT(HOUR FROM o.created_at)
                ORDER BY Hour";

            summary.HourlySales = (await connection.QueryAsync<HourlySale>(hourlySql,
                new { CompanyId = companyId, BranchId = branchId, DateFrom = dateFrom, DateTo = dateTo })).ToList();

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo resumen de ventas");
            throw;
        }
    }

    public async Task<IEnumerable<CompletedOrder>> GetCompletedOrdersAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT o.id AS Id, o.order_number AS OrderNumber,
                       COALESCE(NULLIF(t.name,''), 'Mesa ' || t.table_number) AS TableName,
                       t.table_number AS TableNumber,
                       o.subtotal AS Subtotal, o.discount_amount AS DiscountAmount,
                       o.final_total_paid AS FinalTotalPaid,
                       o.split_reference_count AS SplitReferenceCount,
                       o.created_at AS CreatedAt,
                       CASE WHEN c.id IS NOT NULL THEN TRUE ELSE FALSE END AS HasCredit
                FROM sales.orders o
                JOIN sales.tables t ON t.id = o.table_id
                LEFT JOIN sales.credits c ON c.order_id = o.id AND c.company_id = o.company_id
                WHERE o.company_id = @CompanyId
                  AND o.branch_id  = @BranchId
                  AND o.status     = 'completed'
                  AND COALESCE(o.refund_status, '') <> 'full_refund'
                  AND o.created_at >= @DateFrom
                  AND o.created_at <  @DateTo
                  AND o.deleted_at IS NULL
                ORDER BY o.created_at DESC";

            return await connection.QueryAsync<CompletedOrder>(sql,
                new { CompanyId = companyId, BranchId = branchId, DateFrom = dateFrom, DateTo = dateTo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo ordenes completadas");
            throw;
        }
    }

    public async Task<IEnumerable<Order>> SearchOrdersAsync(long companyId, long branchId,
        DateTime? dateFrom, DateTime? dateTo,
        string? status, string? refundStatus, string? paymentMethod, string? search,
        decimal? minTotal, decimal? maxTotal, string sortBy, string sortDir, int offset, int limit)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var allowedSorts = new HashSet<string> { "created_at", "final_total_paid", "order_number" };
            var col = allowedSorts.Contains(sortBy) ? sortBy : "created_at";
            var dir = sortDir.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

            var sql = $@"
                SELECT o.id AS Id, o.company_id AS CompanyId, o.branch_id AS BranchId,
                       o.table_id AS TableId, o.order_number AS OrderNumber, o.status AS Status,
                       o.subtotal AS Subtotal, o.tax AS Tax, o.total AS Total,
                       o.discount_type AS DiscountType, o.discount_value AS DiscountValue,
                       o.discount_amount AS DiscountAmount, o.final_total_paid AS FinalTotalPaid,
                       o.split_reference_count AS SplitReferenceCount,
                       o.tip_amount AS TipAmount, o.tip_included AS TipIncluded,
                       o.refund_status AS RefundStatus,
                       COALESCE(pay.payment_method_summary, o.payment_method) AS PaymentMethod,
                       o.created_by AS CreatedBy, o.created_at AS CreatedAt,
                       t.name AS TableName, t.table_number AS TableNumber
                FROM sales.orders o
                LEFT JOIN sales.tables t ON t.id = o.table_id
                LEFT JOIN LATERAL (
                    SELECT CASE
                        WHEN COUNT(DISTINCT op.method) = 0 THEN NULL
                        WHEN COUNT(DISTINCT op.method) = 1 THEN MIN(op.method)
                        ELSE 'mixed'
                    END AS payment_method_summary
                    FROM sales.order_payments op
                    WHERE op.order_id = o.id AND op.company_id = o.company_id
                ) pay ON TRUE
                WHERE o.company_id = @CompanyId AND o.branch_id = @BranchId
                  AND o.status IN ('completed','cancelled')
                  {(dateFrom.HasValue ? "AND o.created_at >= @DateFrom" : "")}
                  {(dateTo.HasValue ? "AND o.created_at <= @DateTo" : "")}
                  {(!string.IsNullOrEmpty(status) ? "AND o.status = @Status" : "")}
                  {(!string.IsNullOrEmpty(refundStatus) ? "AND o.refund_status = @RefundStatus" : "")}
                  {(!string.IsNullOrEmpty(paymentMethod) ? "AND EXISTS (SELECT 1 FROM sales.order_payments opf WHERE opf.order_id = o.id AND opf.company_id = o.company_id AND opf.method = @PaymentMethod)" : "")}
                  {(!string.IsNullOrEmpty(search) ? "AND (o.order_number ILIKE @Search OR t.name ILIKE @Search)" : "")}
                  {(minTotal.HasValue ? "AND o.final_total_paid >= @MinTotal" : "")}
                  {(maxTotal.HasValue ? "AND o.final_total_paid <= @MaxTotal" : "")}
                ORDER BY o.{col} {dir}
                LIMIT @Limit OFFSET @Offset";

            return await connection.QueryAsync<Order>(sql, new
            {
                CompanyId = companyId,
                BranchId = branchId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Status = status,
                RefundStatus = refundStatus,
                PaymentMethod = paymentMethod,
                Search = !string.IsNullOrEmpty(search) ? $"%{search}%" : null,
                MinTotal = minTotal,
                MaxTotal = maxTotal,
                Limit = limit,
                Offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error buscando ordenes");
            throw;
        }
    }

    public async Task<int> SearchOrdersCountAsync(long companyId, long branchId,
        DateTime? dateFrom, DateTime? dateTo,
        string? status, string? refundStatus, string? paymentMethod, string? search,
        decimal? minTotal, decimal? maxTotal)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var sql = $@"
                SELECT COUNT(*)
                FROM sales.orders o
                LEFT JOIN sales.tables t ON t.id = o.table_id
                WHERE o.company_id = @CompanyId AND o.branch_id = @BranchId
                  AND o.status IN ('completed','cancelled')
                  {(dateFrom.HasValue ? "AND o.created_at >= @DateFrom" : "")}
                  {(dateTo.HasValue ? "AND o.created_at <= @DateTo" : "")}
                  {(!string.IsNullOrEmpty(status) ? "AND o.status = @Status" : "")}
                  {(!string.IsNullOrEmpty(refundStatus) ? "AND o.refund_status = @RefundStatus" : "")}
                  {(!string.IsNullOrEmpty(paymentMethod) ? "AND EXISTS (SELECT 1 FROM sales.order_payments opf WHERE opf.order_id = o.id AND opf.company_id = o.company_id AND opf.method = @PaymentMethod)" : "")}
                  {(!string.IsNullOrEmpty(search) ? "AND (o.order_number ILIKE @Search OR t.name ILIKE @Search)" : "")}
                  {(minTotal.HasValue ? "AND o.final_total_paid >= @MinTotal" : "")}
                  {(maxTotal.HasValue ? "AND o.final_total_paid <= @MaxTotal" : "")}";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                CompanyId = companyId,
                BranchId = branchId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Status = status,
                RefundStatus = refundStatus,
                PaymentMethod = paymentMethod,
                Search = !string.IsNullOrEmpty(search) ? $"%{search}%" : null,
                MinTotal = minTotal,
                MaxTotal = maxTotal
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error contando ordenes en busqueda");
            throw;
        }
    }
}
