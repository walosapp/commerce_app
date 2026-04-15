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

    public async Task<SalesTable?> GetTableByIdAsync(long tableId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       table_number AS TableNumber, name AS Name, status AS Status,
                       created_by AS CreatedBy, created_at AS CreatedAt
                FROM sales.tables
                WHERE id = @TableId AND company_id = @CompanyId AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<SalesTable>(sql, new { TableId = tableId, CompanyId = companyId });
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

    public async Task<Order?> GetOrderByTableIdAsync(long tableId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       table_id AS TableId, order_number AS OrderNumber, status AS Status,
                       subtotal AS Subtotal, tax AS Tax, total AS Total, notes AS Notes,
                       created_by AS CreatedBy, created_at AS CreatedAt
                FROM sales.orders
                WHERE table_id = @TableId AND company_id = @CompanyId AND status = 'pending'
                ORDER BY created_at DESC";

            return await connection.QueryFirstOrDefaultAsync<Order>(sql, new { TableId = tableId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo orden de mesa {TableId}", tableId);
            throw;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(long orderId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       table_id AS TableId, order_number AS OrderNumber, status AS Status,
                       subtotal AS Subtotal, tax AS Tax, total AS Total, notes AS Notes,
                       created_by AS CreatedBy, created_at AS CreatedAt
                FROM sales.orders
                WHERE id = @OrderId AND company_id = @CompanyId";

            return await connection.QueryFirstOrDefaultAsync<Order>(sql, new { OrderId = orderId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo orden {OrderId}", orderId);
            throw;
        }
    }

    public async Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId, long companyId)
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
                LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id
                WHERE oi.order_id = @OrderId AND oi.company_id = @CompanyId";

            return await connection.QueryAsync<OrderItem>(sql, new { OrderId = orderId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo items de orden {OrderId}", orderId);
            throw;
        }
    }

    public async Task<OrderItem?> GetOrderItemByIdAsync(long itemId, long companyId)
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
                LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id
                WHERE oi.id = @ItemId AND oi.company_id = @CompanyId";

            return await connection.QueryFirstOrDefaultAsync<OrderItem>(sql, new { ItemId = itemId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo item {ItemId}", itemId);
            throw;
        }
    }

    public async Task UpdateTableStatusAsync(long tableId, long companyId, string status)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE sales.tables
                SET status = @Status, updated_at = NOW()
                WHERE id = @TableId AND company_id = @CompanyId";

            await connection.ExecuteAsync(sql, new { TableId = tableId, CompanyId = companyId, Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado de mesa {TableId}", tableId);
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(long orderId, long companyId, string status)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE sales.orders
                SET status = @Status, updated_at = NOW()
                WHERE id = @OrderId AND company_id = @CompanyId";

            await connection.ExecuteAsync(sql, new { OrderId = orderId, CompanyId = companyId, Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado de orden {OrderId}", orderId);
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
    public async Task UpdateOrderInvoiceSummaryAsync(long orderId, long companyId, string? discountType, decimal discountValue, decimal discountAmount, decimal finalTotalPaid, int splitReferenceCount)
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
                SplitReferenceCount = splitReferenceCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando resumen de factura {OrderId}", orderId);
            throw;
        }
    }
}
