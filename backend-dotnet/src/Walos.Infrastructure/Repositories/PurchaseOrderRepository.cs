using Dapper;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IDbConnectionFactory _db;
    public PurchaseOrderRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<PurchaseOrderResponse>> GetAllAsync(long companyId, long? branchId, long? supplierId = null)
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
                po.created_at AS CreatedAt,
                s.phone AS SupplierPhone, s.email AS SupplierEmail
            FROM suppliers.purchase_orders po
            JOIN suppliers.suppliers s
              ON s.id = po.supplier_id
             AND s.company_id = po.company_id
             AND (s.branch_id = po.branch_id OR s.branch_id IS NULL)
            WHERE po.company_id = @CompanyId
              AND (@BranchId::bigint IS NULL OR po.branch_id = @BranchId)";

        if (supplierId.HasValue) sql += " AND po.supplier_id = @SupplierId";
        sql += " ORDER BY po.created_at DESC";

        return await conn.QueryAsync<PurchaseOrderResponse>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId
        });
    }

    public async Task<PurchaseOrderResponse?> GetByIdAsync(long id, long companyId, long? branchId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string orderSql = @"
            SELECT po.id AS Id, po.company_id AS CompanyId, po.branch_id AS BranchId,
                   po.supplier_id AS SupplierId, s.name AS SupplierName,
                   po.order_number AS OrderNumber, po.status AS Status,
                   po.notes AS Notes, po.expected_date AS ExpectedDate,
                   po.received_at AS ReceivedAt,
                   po.subtotal AS Subtotal, po.tax AS Tax, po.total AS Total,
                   po.created_at AS CreatedAt,
                   s.phone AS SupplierPhone, s.email AS SupplierEmail
            FROM suppliers.purchase_orders po
            JOIN suppliers.suppliers s
              ON s.id = po.supplier_id
             AND s.company_id = po.company_id
             AND (s.branch_id = po.branch_id OR s.branch_id IS NULL)
            WHERE po.id = @Id
              AND po.company_id = @CompanyId
              AND (@BranchId::bigint IS NULL OR po.branch_id = @BranchId)";

        var order = await conn.QueryFirstOrDefaultAsync<PurchaseOrderResponse>(orderSql, new
        {
            Id = id,
            CompanyId = companyId,
            BranchId = branchId
        });
        if (order is null) return null;

        const string itemsSql = @"
            SELECT poi.id AS Id, poi.product_id AS ProductId, poi.product_name AS ProductName,
                   poi.quantity AS Quantity, poi.unit_cost AS UnitCost, poi.subtotal AS Subtotal,
                   poi.received_qty AS ReceivedQty
            FROM suppliers.purchase_order_items poi
            JOIN suppliers.purchase_orders po
              ON po.id = poi.order_id
             AND po.company_id = @CompanyId
            WHERE poi.order_id = @OrderId";

        order.Items = (await conn.QueryAsync<PurchaseOrderItemResponse>(itemsSql, new { OrderId = id, CompanyId = companyId })).ToList();
        return order;
    }

    public async Task<PurchaseOrderResponse> CreateAsync(long companyId, long branchId, long userId, CreatePurchaseOrderRequest request)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            var validBranch = await conn.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT 1
                    FROM core.branches
                    WHERE id = @BranchId
                      AND company_id = @CompanyId
                      AND is_active = TRUE
                      AND deleted_at IS NULL
                    FOR SHARE
                )", new { BranchId = branchId, CompanyId = companyId }, tx);
            if (!validBranch)
                throw new NotFoundException("Sucursal no encontrada");

            var supplier = await conn.QuerySingleOrDefaultAsync<PurchaseOrderSupplier>(@"
                SELECT id AS Id, name AS Name, phone AS Phone, email AS Email
                FROM suppliers.suppliers
                WHERE id = @SupplierId
                  AND company_id = @CompanyId
                  AND (branch_id = @BranchId OR branch_id IS NULL)
                  AND is_active = TRUE
                  AND deleted_at IS NULL
                FOR SHARE",
                new { request.SupplierId, CompanyId = companyId, BranchId = branchId }, tx)
                ?? throw new NotFoundException("Proveedor no encontrado");

            var productIds = request.Items.Select(item => item.ProductId).Distinct().ToArray();
            var products = (await conn.QueryAsync<PurchaseOrderProduct>(@"
                SELECT id AS Id, name AS Name
                FROM inventory.products
                WHERE company_id = @CompanyId
                  AND id = ANY(@ProductIds)
                  AND is_active = TRUE
                  AND deleted_at IS NULL
                ORDER BY id
                FOR SHARE",
                new { CompanyId = companyId, ProductIds = productIds }, tx)).ToDictionary(product => product.Id);

            if (products.Count != productIds.Length)
                throw new NotFoundException("Uno o mas productos no fueron encontrados");

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
                BranchId = branchId,
                request.SupplierId,
                OrderNumber = orderNumber,
                request.Notes,
                request.ExpectedDate,
                Subtotal = subtotal,
                CreatedBy = userId
            }, tx);

            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];
                var itemSubtotal = item.Quantity * item.UnitCost;
                await conn.ExecuteAsync(@"
                    INSERT INTO suppliers.purchase_order_items
                        (order_id, product_id, product_name, quantity, unit_cost, subtotal)
                    VALUES (@OrderId, @ProductId, @ProductName, @Quantity, @UnitCost, @Subtotal)",
                    new { OrderId = order.Id, item.ProductId, ProductName = product.Name, item.Quantity, item.UnitCost, Subtotal = itemSubtotal }, tx);
            }

            order.SupplierName = supplier.Name;
            order.SupplierPhone = supplier.Phone;
            order.SupplierEmail = supplier.Email;
            order.Items = request.Items.Select(i => new PurchaseOrderItemResponse
            {
                ProductId = i.ProductId, ProductName = products[i.ProductId].Name,
                Quantity = i.Quantity, UnitCost = i.UnitCost, Subtotal = i.Quantity * i.UnitCost
            }).ToList();

            tx.Commit();

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
                SELECT po.id AS Id, po.company_id AS CompanyId, po.branch_id AS BranchId,
                       po.supplier_id AS SupplierId, po.status AS Status, po.total AS Total
                FROM suppliers.purchase_orders po
                JOIN core.branches b
                  ON b.id = po.branch_id
                 AND b.company_id = po.company_id
                JOIN suppliers.suppliers s
                  ON s.id = po.supplier_id
                 AND s.company_id = po.company_id
                 AND (s.branch_id = po.branch_id OR s.branch_id IS NULL)
                WHERE po.id = @Id
                  AND po.company_id = @CompanyId
                  AND po.branch_id = @BranchId
                FOR UPDATE OF po
                FOR SHARE OF b, s",
                new { Id = id, CompanyId = companyId, BranchId = branchId }, tx)
                ?? throw new InvalidOperationException("Pedido no encontrado");

            if (order.Status == "received")
                throw new InvalidOperationException("El pedido ya fue recibido");
            if (order.Status == "cancelled")
                throw new InvalidOperationException("El pedido fue cancelado");

            // Lock and validate every order item and product before making changes.
            var items = (await conn.QueryAsync<ReceivablePurchaseOrderItem>(@"
                SELECT poi.id AS Id, poi.product_id AS ProductId,
                       poi.product_name AS ProductName, poi.quantity AS Quantity,
                       poi.unit_cost AS UnitCost, poi.subtotal AS Subtotal,
                       poi.received_qty AS ReceivedQty,
                       p.company_id AS ProductCompanyId
                FROM suppliers.purchase_order_items poi
                JOIN inventory.products p ON p.id = poi.product_id
                WHERE poi.order_id = @OrderId
                FOR UPDATE OF poi
                FOR SHARE OF p",
                new { OrderId = id }, tx)).ToList();

            if (items.Any(item => item.ProductCompanyId != companyId))
                throw new InvalidOperationException("El pedido contiene productos de otro comercio");

            var itemsById = items.ToDictionary(item => item.Id);
            if (request.Items.Select(item => item.OrderItemId).Distinct().Count() != request.Items.Count)
                throw new InvalidOperationException("La recepcion contiene items duplicados");

            if (request.Items.Any(item => !itemsById.ContainsKey(item.OrderItemId)))
                throw new InvalidOperationException("Uno o mas items no pertenecen al pedido");

            if (request.Items.Any(item => item.ReceivedQty < 0))
                throw new InvalidOperationException("La cantidad recibida no puede ser negativa");

            // Update received quantities with order and tenant isolation.
            foreach (var recv in request.Items)
            {
                var updatedRows = await conn.ExecuteAsync(@"
                    UPDATE suppliers.purchase_order_items poi
                    SET received_qty = @ReceivedQty
                    FROM suppliers.purchase_orders po
                    WHERE poi.id = @OrderItemId
                      AND poi.order_id = @OrderId
                      AND po.id = poi.order_id
                      AND po.company_id = @CompanyId",
                    new
                    {
                        recv.ReceivedQty,
                        recv.OrderItemId,
                        OrderId = id,
                        CompanyId = companyId
                    }, tx);

                if (updatedRows != 1)
                    throw new InvalidOperationException("No fue posible actualizar el item del pedido");

                itemsById[recv.OrderItemId].ReceivedQty = recv.ReceivedQty;
            }

            // Update stock for each item using received quantity
            foreach (var item in items)
            {
                var qty = item.ReceivedQty ?? item.Quantity;
                if (qty <= 0) continue;

                // Upsert stock
                var stockAfter = await conn.QuerySingleAsync<decimal>(@"
                    INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
                    VALUES (@CompanyId, @BranchId, @ProductId, @Qty)
                    ON CONFLICT (branch_id, product_id)
                    DO UPDATE SET quantity = inventory.stock.quantity + @Qty, updated_at = NOW()
                    WHERE inventory.stock.company_id = @CompanyId
                    RETURNING quantity",
                    new { CompanyId = companyId, BranchId = branchId, item.ProductId, Qty = qty }, tx);

                // Stock movement
                await conn.ExecuteAsync(@"
                    INSERT INTO inventory.movements
                        (company_id, branch_id, product_id, movement_type, quantity, unit_cost,
                         reference_type, reference_id, notes, stock_after, created_by)
                    VALUES
                        (@CompanyId, @BranchId, @ProductId, 'purchase', @Qty, @UnitCost,
                         'purchase_order', @ReferenceId, @Notes, @StockAfter, @CreatedBy)",
                    new
                    {
                        CompanyId = companyId, BranchId = branchId,
                        item.ProductId, Qty = qty, item.UnitCost,
                        ReferenceId = id,
                        Notes = string.IsNullOrWhiteSpace(request.Notes)
                            ? $"Recepcion pedido #{id}"
                            : request.Notes,
                        StockAfter = stockAfter,
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
            var receivedRows = await conn.ExecuteAsync(@"
                UPDATE suppliers.purchase_orders
                SET status = 'received', received_at = NOW(), received_by = @UserId, updated_at = NOW()
                WHERE id = @Id
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId",
                new { Id = id, CompanyId = companyId, BranchId = branchId, UserId = userId }, tx);

            if (receivedRows != 1)
                throw new InvalidOperationException("No fue posible marcar el pedido como recibido");

            tx.Commit();

            return (await GetByIdAsync(id, companyId, branchId))!;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<bool> CancelAsync(long id, long companyId, long? branchId)
    {
        using var conn = await _db.CreateConnectionAsync();
        var rows = await conn.ExecuteAsync(@"
            UPDATE suppliers.purchase_orders SET status = 'cancelled', updated_at = NOW()
            WHERE id = @Id
              AND company_id = @CompanyId
              AND (@BranchId::bigint IS NULL OR branch_id = @BranchId)
              AND status = 'pending'",
            new { Id = id, CompanyId = companyId, BranchId = branchId });
        return rows > 0;
    }

    private sealed class ReceivablePurchaseOrderItem : PurchaseOrderItemResponse
    {
        public long ProductCompanyId { get; set; }
    }

    private sealed class PurchaseOrderProduct
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PurchaseOrderSupplier
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
