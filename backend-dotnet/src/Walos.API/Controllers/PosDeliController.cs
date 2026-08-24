using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.PosDeli;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/pos-deli")]
[Authorize(Roles = "dev,super_admin,admin,manager,cashier")]
public class PosDeliController : ControllerBase
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PosDeliController> _logger;

    public PosDeliController(
        IDbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        ILogger<PosDeliController> logger)
    {
        _connectionFactory = connectionFactory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] string? barcode,
        [FromQuery] long? categoryId)
    {
        var branchId = _tenantContext.BranchId;
        if (!branchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        using var connection = await _connectionFactory.CreateConnectionAsync();

        var sql = @"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.sale_price AS SalePrice,
                p.product_type AS ProductType,
                p.track_stock AS TrackStock,
                p.image_url AS ImageUrl,
                c.name AS CategoryName,
                u.name AS UnitName,
                u.abbreviation AS UnitAbbreviation,
                COALESCE(s.quantity, 0) AS Quantity,
                COALESCE(s.quantity, 0) AS AvailableQuantity
            FROM inventory.products p
            INNER JOIN inventory.categories c ON c.id = p.category_id AND c.company_id = p.company_id
            INNER JOIN inventory.units u ON u.id = p.unit_id AND u.company_id = p.company_id
            LEFT JOIN inventory.stock s ON s.company_id = p.company_id AND s.product_id = p.id AND s.branch_id = @BranchId
            WHERE p.company_id = @CompanyId
              AND p.deleted_at IS NULL
              AND p.is_active = TRUE
              AND p.is_for_sale = TRUE";

        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", _tenantContext.CompanyId);
        parameters.Add("BranchId", branchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (p.name ILIKE @Search OR p.sku ILIKE @Search OR COALESCE(p.barcode, '') ILIKE @Search)";
            parameters.Add("Search", $"%{search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            sql += " AND p.barcode = @Barcode";
            parameters.Add("Barcode", barcode.Trim());
        }

        if (categoryId.HasValue)
        {
            sql += " AND p.category_id = @CategoryId";
            parameters.Add("CategoryId", categoryId.Value);
        }

        sql += " ORDER BY c.name, p.name";

        var items = (await connection.QueryAsync<PosDeliProductResponse>(sql, parameters)).ToList();

        if (!string.IsNullOrWhiteSpace(barcode) && items.Count == 0)
            return NotFound(ApiResponse.Fail($"Producto no encontrado: {barcode}"));

        return Ok(ApiResponse<List<PosDeliProductResponse>>.Ok(items, count: items.Count));
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites()
    {
        var branchId = _tenantContext.BranchId;
        if (!branchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.sale_price AS SalePrice,
                p.product_type AS ProductType,
                p.track_stock AS TrackStock,
                p.image_url AS ImageUrl,
                c.name AS CategoryName,
                u.name AS UnitName,
                u.abbreviation AS UnitAbbreviation,
                COALESCE(s.quantity, 0) AS Quantity,
                COALESCE(s.quantity, 0) AS AvailableQuantity
            FROM inventory.products p
            INNER JOIN inventory.categories c ON c.id = p.category_id AND c.company_id = p.company_id
            INNER JOIN inventory.units u ON u.id = p.unit_id AND u.company_id = p.company_id
            LEFT JOIN inventory.stock s ON s.company_id = p.company_id AND s.product_id = p.id AND s.branch_id = @BranchId
            WHERE p.company_id = @CompanyId
              AND p.deleted_at IS NULL
              AND p.is_active = TRUE
              AND p.is_for_sale = TRUE
            ORDER BY
                CASE
                    WHEN LOWER(COALESCE(u.abbreviation, '')) IN ('kg', 'g', 'gr', 'lb') THEN 0
                    ELSE 1
                END,
                p.name
            LIMIT 24";

        var items = (await connection.QueryAsync<PosDeliProductResponse>(sql, new
        {
            CompanyId = _tenantContext.CompanyId,
            BranchId = branchId.Value
        })).ToList();

        return Ok(ApiResponse<List<PosDeliProductResponse>>.Ok(items, count: items.Count));
    }

    [HttpPost("sale")]
    public async Task<IActionResult> CreateSale([FromBody] PosDeliSaleRequest request)
    {
        var branchId = _tenantContext.BranchId;
        if (!branchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        if (request.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("La venta debe tener al menos un producto"));

        if (request.Items.Any(i => i.ProductId <= 0 || i.Quantity <= 0))
            return BadRequest(ApiResponse.Fail("Todos los items deben tener producto y cantidad válidos"));

        if (request.Payments.Count == 0)
            return BadRequest(ApiResponse.Fail("Debe especificar al menos un método de pago"));

        if (request.Payments.Any(p => p.Amount <= 0 || string.IsNullOrWhiteSpace(p.Method)))
            return BadRequest(ApiResponse.Fail("Todos los pagos deben tener método y monto mayor a cero"));

        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string cashSettingsSql = @"
                SELECT require_cash_register
                FROM core.companies
                WHERE id = @CompanyId AND is_active = TRUE";

            var requireCashRegister = await connection.ExecuteScalarAsync<bool?>(cashSettingsSql, new
            {
                CompanyId = _tenantContext.CompanyId
            }, transaction) ?? throw new ValidationException("Comercio no encontrado o inactivo");

            const string activeRegisterSql = @"
                SELECT id
                FROM sales.cash_registers
                WHERE company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND opened_by = @UserId
                  AND status = 'open'
                  AND deleted_at IS NULL
                ORDER BY opened_at DESC
                LIMIT 1
                FOR UPDATE";

            var cashRegisterId = await connection.QueryFirstOrDefaultAsync<long?>(activeRegisterSql, new
            {
                CompanyId = _tenantContext.CompanyId,
                BranchId = branchId.Value,
                UserId = _tenantContext.UserId
            }, transaction);

            if (requireCashRegister && !cashRegisterId.HasValue)
                throw new BusinessException("Debes abrir una caja antes de registrar ventas");

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToArray();
            const string productSql = @"
                SELECT
                    p.id AS Id,
                    p.name AS Name,
                    p.sale_price AS SalePrice,
                    p.cost_price AS CostPrice,
                    p.track_stock AS TrackStock,
                    COALESCE(s.quantity, 0) AS AvailableQuantity
                FROM inventory.products p
                LEFT JOIN inventory.stock s ON s.company_id = p.company_id AND s.product_id = p.id AND s.branch_id = @BranchId
                WHERE p.company_id = @CompanyId
                  AND p.id = ANY(@ProductIds)
                  AND p.deleted_at IS NULL";

            var products = (await connection.QueryAsync(productSql, new
            {
                CompanyId = _tenantContext.CompanyId,
                BranchId = branchId.Value,
                ProductIds = productIds
            }, transaction)).ToDictionary(x => (long)x.id);

            foreach (var item in request.Items)
            {
                if (!products.ContainsKey(item.ProductId))
                    throw new ValidationException($"Producto no encontrado: {item.ProductId}");

                var product = products[item.ProductId];
                if ((bool)product.trackstock && (decimal)product.availablequantity < item.Quantity)
                    throw new BusinessException($"Stock insuficiente para {product.name}");
            }

            var normalizedItems = request.Items.Select(item =>
            {
                var product = products[item.ProductId];
                var unitPrice = Math.Round((decimal)product.saleprice, 2);
                var subtotal = Math.Round(item.Quantity * unitPrice, 2);

                return new
                {
                    item.ProductId,
                    ProductName = (string)product.name,
                    item.Quantity,
                    UnitPrice = unitPrice,
                    Subtotal = subtotal,
                    TrackStock = (bool)product.trackstock,
                    CostPrice = (decimal)product.costprice
                };
            }).ToList();

            var total = normalizedItems.Sum(i => i.Subtotal);
            var paymentTotal = request.Payments.Sum(p => p.Amount);
            if (Math.Abs(paymentTotal - total) > 1)
                throw new ValidationException($"La suma de pagos ({paymentTotal:N2}) no coincide con el total ({total:N2})");

            var orderNumber = $"POS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
            var tableNumber = Random.Shared.Next(900000, 999999);

            const string tableSql = @"
                INSERT INTO sales.tables (company_id, branch_id, table_number, name, status, created_by, created_at)
                VALUES (@CompanyId, @BranchId, @TableNumber, @Name, 'invoiced', @CreatedBy, NOW())
                RETURNING id";

            var tableId = await connection.ExecuteScalarAsync<long>(tableSql, new
            {
                CompanyId = _tenantContext.CompanyId,
                BranchId = branchId.Value,
                TableNumber = tableNumber,
                Name = $"POS DELI {orderNumber}",
                CreatedBy = _tenantContext.UserId
            }, transaction);

            const string orderSql = @"
                INSERT INTO sales.orders (
                    company_id, branch_id, table_id, order_number, status,
                    subtotal, tax, total, discount_amount, final_total_paid,
                    split_reference_count, notes, created_by, payment_method,
                    tip_amount, tip_included, cash_register_id, created_at
                ) VALUES (
                    @CompanyId, @BranchId, @TableId, @OrderNumber, 'completed',
                    @Subtotal, 0, @Total, 0, @FinalTotalPaid,
                    1, @Notes, @CreatedBy, @PaymentMethod,
                    0, FALSE, @CashRegisterId, NOW()
                )
                RETURNING id";

            var paymentMethod = ResolvePaymentMethod(request.Payments);
            var orderId = await connection.ExecuteScalarAsync<long>(orderSql, new
            {
                CompanyId = _tenantContext.CompanyId,
                BranchId = branchId.Value,
                TableId = tableId,
                OrderNumber = orderNumber,
                Subtotal = total,
                Total = total,
                FinalTotalPaid = total,
                Notes = request.Notes,
                CreatedBy = _tenantContext.UserId,
                PaymentMethod = paymentMethod,
                CashRegisterId = cashRegisterId
            }, transaction);

            const string itemSql = @"
                INSERT INTO sales.order_items (company_id, order_id, product_id, product_name, quantity, unit_price, created_at)
                VALUES (@CompanyId, @OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, NOW())";

            foreach (var item in normalizedItems)
            {
                await connection.ExecuteAsync(itemSql, new
                {
                    CompanyId = _tenantContext.CompanyId,
                    OrderId = orderId,
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice
                }, transaction);
            }

            const string paymentSql = @"
                INSERT INTO sales.order_payments (company_id, order_id, method, amount, reference, created_at)
                VALUES (@CompanyId, @OrderId, @Method, @Amount, @Reference, NOW())";

            foreach (var payment in request.Payments)
            {
                await connection.ExecuteAsync(paymentSql, new
                {
                    CompanyId = _tenantContext.CompanyId,
                    OrderId = orderId,
                    Method = payment.Method.Trim().ToLowerInvariant(),
                    payment.Amount,
                    payment.Reference
                }, transaction);
            }

            if (cashRegisterId.HasValue)
            {
                var totalCashSales = request.Payments
                    .Where(p => p.Method.Trim().Equals("cash", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var totalCardSales = request.Payments
                    .Where(p => p.Method.Trim().Equals("card", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var totalTransferSales = request.Payments
                    .Where(p => p.Method.Trim().Equals("transfer", StringComparison.OrdinalIgnoreCase)
                             || p.Method.Trim().Equals("nequi", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);
                var totalOtherSales = request.Payments
                    .Where(p => !new[] { "cash", "card", "transfer", "nequi" }
                        .Contains(p.Method.Trim().ToLowerInvariant()))
                    .Sum(p => p.Amount);

                const string updateRegisterSql = @"
                    UPDATE sales.cash_registers
                    SET total_sales = total_sales + @TotalSales,
                        total_cash_sales = total_cash_sales + @TotalCashSales,
                        total_card_sales = total_card_sales + @TotalCardSales,
                        total_transfer_sales = total_transfer_sales + @TotalTransferSales,
                        total_other_sales = total_other_sales + @TotalOtherSales,
                        order_count = order_count + 1,
                        updated_at = NOW()
                    WHERE id = @CashRegisterId
                      AND company_id = @CompanyId
                      AND branch_id = @BranchId
                      AND status = 'open'";

                var updatedRegisters = await connection.ExecuteAsync(updateRegisterSql, new
                {
                    CashRegisterId = cashRegisterId.Value,
                    CompanyId = _tenantContext.CompanyId,
                    BranchId = branchId.Value,
                    TotalSales = total,
                    TotalCashSales = totalCashSales,
                    TotalCardSales = totalCardSales,
                    TotalTransferSales = totalTransferSales,
                    TotalOtherSales = totalOtherSales
                }, transaction);

                if (updatedRegisters != 1)
                    throw new BusinessException("La caja dejó de estar disponible durante la venta");
            }

            const string stockSql = @"
                UPDATE inventory.stock
                SET quantity = quantity - @Quantity,
                    updated_at = NOW()
                WHERE company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND product_id = @ProductId";

            const string movementSql = @"
                INSERT INTO inventory.movements (
                    company_id, branch_id, product_id, movement_type, quantity, unit_cost, notes, created_by, created_at
                ) VALUES (
                    @CompanyId, @BranchId, @ProductId, 'sale', @Quantity, @UnitCost, @Notes, @CreatedBy, NOW()
                )";

            foreach (var item in normalizedItems)
            {
                if (item.TrackStock)
                {
                    await connection.ExecuteAsync(stockSql, new
                    {
                        CompanyId = _tenantContext.CompanyId,
                        BranchId = branchId.Value,
                        item.ProductId,
                        item.Quantity
                    }, transaction);
                }

                await connection.ExecuteAsync(movementSql, new
                {
                    CompanyId = _tenantContext.CompanyId,
                    BranchId = branchId.Value,
                    item.ProductId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitPrice,
                    Notes = $"Venta POS Deli - {orderNumber}",
                    CreatedBy = _tenantContext.UserId
                }, transaction);
            }

            transaction.Commit();

            var cashReceived = request.CashReceived ?? 0;
            var change = Math.Max(0, Math.Round(cashReceived - total, 2));

            _logger.LogInformation("Venta POS-Deli creada. OrderId={OrderId}, Total={Total}", orderId, total);

            return Ok(ApiResponse<PosDeliSaleResponse>.Ok(new PosDeliSaleResponse
            {
                SaleId = orderId,
                TicketNumber = orderNumber,
                Total = total,
                Change = change
            }, "Venta registrada exitosamente"));
        }
        catch (Exception ex) when (ex is ValidationException or BusinessException)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error creando venta POS-Deli");
            throw;
        }
    }

    private static string ResolvePaymentMethod(IEnumerable<PosDeliPayment> payments)
    {
        var methods = payments
            .Where(p => p.Amount > 0 && !string.IsNullOrWhiteSpace(p.Method))
            .Select(p => p.Method.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        return methods.Count == 1 ? methods[0] : "mixed";
    }
}
