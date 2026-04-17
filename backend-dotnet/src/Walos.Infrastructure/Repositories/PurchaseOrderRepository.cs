using Dapper;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IDbConnectionFactory _db;
    public PurchaseOrderRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<PurchaseOrderResponse>> GetAllAsync(long companyId, long? supplierId = null)
    {
        using var conn = await _db.CreateConnectionAsync();
        var sql = @"
            SELECT
                po.id AS Id, po.company_id AS CompanyId, po.branch_id AS BranchId,
                po.supplier_id AS SupplierId, s.name AS SupplierName,
                po.order_number AS OrderNumber, po.status AS Status,
                po.notes AS Notes, po.expected_date AS ExpectedDate,
                po.received_at AS ReceivedAt,
                po.subtotal AS Subtotal, po.tax AS Tax, po.total AS Total,
                po.created_at AS CreatedAt
            FROM suppliers.purchase_orders po
            JOIN suppliers.suppliers s ON s.id = po.supplier_id
            WHERE po.company_id = @CompanyId";

        if (supplierId.HasValue) sql += " AND po.supplier_id = @SupplierId";
        sql += " ORDER BY po.created_at DESC";

        return await conn.QueryAsync<PurchaseOrderResponse>(sql, new { CompanyId = companyId, SupplierId = supplierId });
    }

    public async Task<PurchaseOrderResponse?> GetByIdAsync(long id, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string orderSql = @"
            SELECT po.id AS Id, po.company_id AS CompanyId, po.branch_id AS BranchId,
                   po.supplier_id AS SupplierId, s.name AS SupplierName,
                   po.order_number AS OrderNumber, po.status AS Status,
                   po.notes AS Notes, po.expected_date AS ExpectedDate,
                   po.received_at AS ReceivedAt,
                   po.subtotal AS Subtotal, po.tax AS Tax, po.total AS Total,
                   po.created_at AS CreatedAt
            FROM suppliers.purchase_orders po
            JOIN suppliers.suppliers s ON s.id = po.supplier_id
            WHERE po.id = @Id AND po.company_id = @CompanyId";

        var order = await conn.QueryFirstOrDefaultAsync<PurchaseOrderResponse>(orderSql, new { Id = id, CompanyId = companyId });
        if (order is null) return null;

        const string itemsSql = @"
            SELECT id AS Id, product_id AS ProductId, product_name AS ProductName,
                   quantity AS Quantity, unit_cost AS UnitCost, subtotal AS Subtotal,
                   received_qty AS ReceivedQty
            FROM suppliers.purchase_order_items WHERE order_id = @OrderId";

        order.Items = (await conn.QueryAsync<PurchaseOrderItemResponse>(itemsSql, new { OrderId = id })).ToList();
        return order;
    }

    public async Task<PurchaseOrderResponse> CreateAsync(long companyId, long userId, CreatePurchaseOrderRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            var subtotal = request.Items.Sum(i => i.Quantity * i.UnitCost);
            var orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

            const string orderSql = @"
                INSERT INTO suppliers.purchase_orders
                    (company_id, branch_id, supplier_id, order_number, status, notes, expected_date, subtotal, total, created_by)
                VALUES
                    (@CompanyId, @BranchId, @SupplierId, @OrderNumber, 'pending', @Notes, @ExpectedDate, @Subtotal, @Subtotal, @CreatedBy)
                RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                          supplier_id AS SupplierId, order_number AS OrderNumber,
                          status AS Status, notes AS Notes, expected_date AS ExpectedDate,
                          subtotal AS Subtotal, tax AS Tax, total AS Total, created_at AS CreatedAt";

            var order = await conn.QuerySingleAsync<PurchaseOrderResponse>(orderSql, new
            {
                CompanyId = companyId,
                request.BranchId,
                request.SupplierId,
                OrderNumber = orderNumber,
                request.Notes,
                request.ExpectedDate,
                Subtotal = subtotal,
                CreatedBy = userId
            }, tx);

            foreach (var item in request.Items)
            {
                var itemSubtotal = item.Quantity * item.UnitCost;
                await conn.ExecuteAsync(@"
                    INSERT INTO suppliers.purchase_order_items
                        (order_id, product_id, product_name, quantity, unit_cost, subtotal)
                    VALUES (@OrderId, @ProductId, @ProductName, @Quantity, @UnitCost, @Subtotal)",
                    new { OrderId = order.Id, item.ProductId, item.ProductName, item.Quantity, item.UnitCost, Subtotal = itemSubtotal }, tx);
            }

            tx.Commit();

            // Load supplier name
            var supplierName = await conn.ExecuteScalarAsync<string>(
                "SELECT name FROM suppliers.suppliers WHERE id = @Id", new { Id = request.SupplierId });
            order.SupplierName = supplierName ?? "";
            order.Items = request.Items.Select(i => new PurchaseOrderItemResponse
            {
                ProductId = i.ProductId, ProductName = i.ProductName,
                Quantity = i.Quantity, UnitCost = i.UnitCost, Subtotal = i.Quantity * i.UnitCost
            }).ToList();

            return order;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<PurchaseOrderResponse> ReceiveAsync(long id, long companyId, long branchId, long userId, ReceivePurchaseOrderRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            var order = await conn.QueryFirstOrDefaultAsync<PurchaseOrderResponse>(@"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       supplier_id AS SupplierId, status AS Status, total AS Total
                FROM suppliers.purchase_orders
                WHERE id = @Id AND company_id = @CompanyId",
                new { Id = id, CompanyId = companyId }, tx)
                ?? throw new InvalidOperationException("Pedido no encontrado");

            if (order.Status == "received")
                throw new InvalidOperationException("El pedido ya fue recibido");
            if (order.Status == "cancelled")
                throw new InvalidOperationException("El pedido fue cancelado");

            // Update received quantities on items
            foreach (var recv in request.Items)
            {
                await conn.ExecuteAsync(@"
                    UPDATE suppliers.purchase_order_items
                    SET received_qty = @ReceivedQty
                    WHERE id = @Id",
                    new { recv.ReceivedQty, recv.OrderItemId }, tx);
            }

            // Load items to update stock
            var items = (await conn.QueryAsync<PurchaseOrderItemResponse>(@"
                SELECT id AS Id, product_id AS ProductId, product_name AS ProductName,
                       quantity AS Quantity, unit_cost AS UnitCost,
                       COALESCE(received_qty, quantity) AS ReceivedQty
                FROM suppliers.purchase_order_items WHERE order_id = @OrderId",
                new { OrderId = id }, tx)).ToList();

            // Update stock for each item using received quantity
            foreach (var item in items)
            {
                var qty = item.ReceivedQty ?? item.Quantity;
                if (qty <= 0) continue;

                // Upsert stock
                await conn.ExecuteAsync(@"
                    INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
                    VALUES (@CompanyId, @BranchId, @ProductId, @Qty)
                    ON CONFLICT (branch_id, product_id)
                    DO UPDATE SET quantity = inventory.stock.quantity + @Qty, updated_at = NOW()",
                    new { CompanyId = companyId, BranchId = branchId, item.ProductId, Qty = qty }, tx);

                // Stock movement
                await conn.ExecuteAsync(@"
                    INSERT INTO inventory.stock_movements
                        (company_id, branch_id, product_id, movement_type, quantity, unit_cost, notes, created_by)
                    VALUES (@CompanyId, @BranchId, @ProductId, 'purchase', @Qty, @UnitCost, @Notes, @CreatedBy)",
                    new
                    {
                        CompanyId = companyId, BranchId = branchId,
                        item.ProductId, Qty = qty, item.UnitCost,
                        Notes = $"Recepcion pedido #{id}",
                        CreatedBy = userId
                    }, tx);
            }

            // Register finance expense
            var finCatId = await conn.ExecuteScalarAsync<long?>(@"
                SELECT id FROM finance.categories
                WHERE company_id = @CompanyId AND type = 'expense' AND name ILIKE '%insumo%'
                LIMIT 1", new { CompanyId = companyId }, tx);

            if (finCatId.HasValue)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO finance.entries
                        (company_id, branch_id, category_id, type, amount, description, entry_date, created_by)
                    VALUES (@CompanyId, @BranchId, @CategoryId, 'expense', @Amount, @Description, NOW(), @CreatedBy)",
                    new
                    {
                        CompanyId = companyId, BranchId = branchId,
                        CategoryId = finCatId.Value, Amount = order.Total,
                        Description = $"Compra proveedor - Pedido #{id}",
                        CreatedBy = userId
                    }, tx);
            }

            // Mark order as received
            await conn.ExecuteAsync(@"
                UPDATE suppliers.purchase_orders
                SET status = 'received', received_at = NOW(), received_by = @UserId, updated_at = NOW()
                WHERE id = @Id",
                new { Id = id, UserId = userId }, tx);

            tx.Commit();

            return (await GetByIdAsync(id, companyId))!;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<bool> CancelAsync(long id, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        var rows = await conn.ExecuteAsync(@"
            UPDATE suppliers.purchase_orders SET status = 'cancelled', updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND status = 'pending'",
            new { Id = id, CompanyId = companyId });
        return rows > 0;
    }
}
