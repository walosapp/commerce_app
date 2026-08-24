using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.PosDeli;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class PosDeliCashRegisterIntegrationTests : IntegrationTestBase
{
    private sealed class PersistedSaleItem
    {
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Subtotal { get; init; }
    }

    private sealed class PersistedPayment
    {
        public string Method { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }

    [SkippableFact]
    public async Task GetProducts_ByBarcode_Returns_Only_SameTenant_Product()
    {
        var tenantA = await SeedPosContextAsync("Barcode A");
        var tenantB = await SeedPosContextAsync("Barcode B");
        var barcode = $"770{Random.Shared.NextInt64(1000000000, 9999999999)}";
        var productA = await SeedProductAsync(
            tenantA.Company, "Producto barcode A", 25m, barcode: barcode);
        var productB = await SeedProductAsync(
            tenantB.Company, "Producto barcode B", 99m, barcode: barcode);
        var controller = CreateController(tenantA.Company, tenantA.Branch, tenantA.User);

        var result = await controller.GetProducts(search: null, barcode: barcode, categoryId: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<List<PosDeliProductResponse>>>(ok.Value);
        var selected = Assert.Single(response.Data!);
        Assert.Equal(productA, selected.Id);
        Assert.Equal(barcode, selected.Barcode);
        Assert.NotEqual(productB, selected.Id);
    }

    [SkippableFact]
    public async Task CreateSale_Associates_Open_Register_And_Updates_Totals_Atomically()
    {
        var context = await SeedPosContextAsync("POS cash");
        var productId = await SeedProductAsync(context.Company, "Producto POS", 100m);
        var register = await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            OpenedBy = context.User,
            Status = "open",
            OpeningAmount = 25m,
            OpenedAt = DateTime.UtcNow
        });
        var controller = CreateController(context.Company, context.Branch, context.User);

        var result = await controller.CreateSale(new PosDeliSaleRequest
        {
            Items = [new PosDeliSaleItem { ProductId = productId, Quantity = 1 }],
            Payments =
            [
                new PosDeliPayment { Method = "cash", Amount = 40m },
                new PosDeliPayment { Method = "card", Amount = 60m }
            ]
        });

        Assert.IsType<OkObjectResult>(result);
        var persisted = await CashRegisterRepository.GetByIdAsync(register.Id, context.Company);
        Assert.NotNull(persisted);
        Assert.Equal(100m, persisted!.TotalSales);
        Assert.Equal(40m, persisted.TotalCashSales);
        Assert.Equal(60m, persisted.TotalCardSales);
        Assert.Equal(1, persisted.OrderCount);

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT cash_register_id
            FROM sales.orders
            WHERE company_id = @companyId
            ORDER BY id DESC
            LIMIT 1", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", context.Company);
        Assert.Equal(register.Id, (long?)await cmd.ExecuteScalarAsync());
    }

    [SkippableFact]
    public async Task CreateSale_When_Register_Is_Required_And_Missing_Rolls_Back_All_Writes()
    {
        var context = await SeedPosContextAsync("POS no cash");
        var productId = await SeedProductAsync(context.Company, "Producto sin caja", 100m);
        var controller = CreateController(context.Company, context.Branch, context.User);

        await Assert.ThrowsAsync<BusinessException>(() => controller.CreateSale(new PosDeliSaleRequest
        {
            Items = [new PosDeliSaleItem { ProductId = productId, Quantity = 1 }],
            Payments = [new PosDeliPayment { Method = "cash", Amount = 100m }]
        }));

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT
                (SELECT COUNT(*) FROM sales.tables WHERE company_id = @companyId) +
                (SELECT COUNT(*) FROM sales.orders WHERE company_id = @companyId) +
                (SELECT COUNT(*) FROM sales.order_payments WHERE company_id = @companyId)", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", context.Company);
        Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync() ?? -1L));
    }

    [SkippableFact]
    public async Task CreateSale_WeightedDecimal_PersistsEconomics_StockPayment_AndCashRegister()
    {
        var context = await SeedPosContextAsync("POS weighted");
        var productId = await SeedProductAsync(
            context.Company,
            "Producto pesado",
            32.40m,
            productType: "weighted",
            trackStock: true,
            unitType: "weight");
        await SeedStockAsync(context.Company, context.Branch, productId, 10m);
        var register = await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            OpenedBy = context.User,
            Status = "open",
            OpeningAmount = 20m,
            OpenedAt = DateTime.UtcNow
        });
        var controller = CreateController(context.Company, context.Branch, context.User);

        var result = await controller.CreateSale(new PosDeliSaleRequest
        {
            Items = [new PosDeliSaleItem { ProductId = productId, Quantity = 1.25m, IsWeighed = true }],
            Payments = [new PosDeliPayment { Method = "cash", Amount = 40.50m }]
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<PosDeliSaleResponse>>(ok.Value);
        Assert.Equal(40.50m, response.Data!.Total);

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var item = await conn.QuerySingleAsync<PersistedSaleItem>(@"
            SELECT quantity AS Quantity, unit_price AS UnitPrice, subtotal AS Subtotal
            FROM sales.order_items
            WHERE company_id = @CompanyId AND order_id = @OrderId AND product_id = @ProductId",
            new { CompanyId = context.Company, OrderId = response.Data.SaleId, ProductId = productId });
        var payment = await conn.QuerySingleAsync<PersistedPayment>(@"
            SELECT method AS Method, amount AS Amount
            FROM sales.order_payments
            WHERE company_id = @CompanyId AND order_id = @OrderId",
            new { CompanyId = context.Company, OrderId = response.Data.SaleId });
        var remainingStock = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT quantity
            FROM inventory.stock
            WHERE company_id = @CompanyId AND branch_id = @BranchId AND product_id = @ProductId",
            new { CompanyId = context.Company, BranchId = context.Branch, ProductId = productId });

        Assert.Equal(1.25m, item.Quantity);
        Assert.Equal(32.40m, item.UnitPrice);
        Assert.Equal(40.50m, item.Subtotal);
        Assert.Equal("cash", payment.Method);
        Assert.Equal(40.50m, payment.Amount);
        Assert.Equal(8.75m, remainingStock);

        var persistedRegister = await CashRegisterRepository.GetByIdAsync(register.Id, context.Company);
        Assert.NotNull(persistedRegister);
        Assert.Equal(40.50m, persistedRegister!.TotalSales);
        Assert.Equal(40.50m, persistedRegister.TotalCashSales);
        Assert.Equal(1, persistedRegister.OrderCount);
    }

    private PosDeliController CreateController(long companyId, long branchId, long userId) => new(
        ConnectionFactory,
        new TenantContext
        {
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            IsAuthenticated = true
        },
        NullLogger<PosDeliController>.Instance);

    private async Task<(long Company, long Branch, long User)> SeedPosContextAsync(string prefix)
    {
        var company = await SeedCompanyAsync($"{prefix} Company");
        var branch = await SeedBranchAsync(company, $"{prefix} Branch");
        var user = await SeedUserAsync(company, branch, $"{prefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}@test.com");
        return (company, branch, user);
    }

    private async Task<long> SeedProductAsync(
        long companyId,
        string name,
        decimal salePrice,
        string? barcode = null,
        string productType = "simple",
        bool trackStock = false,
        string unitType = "unit")
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var categoryId = await ExecuteScalarAsync(conn, @"
            INSERT INTO inventory.categories (company_id, name, code, is_active, created_by)
            VALUES (@companyId, @name, @code, true, 1)
            RETURNING id", new NpgsqlParameter("@companyId", companyId),
            new NpgsqlParameter("@name", $"Category {name}"),
            new NpgsqlParameter("@code", $"C{suffix}"));
        var unitId = await ExecuteScalarAsync(conn, @"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active, created_by)
            VALUES (@companyId, @name, @abbreviation, @unitType, true, 1)
            RETURNING id", new NpgsqlParameter("@companyId", companyId),
            new NpgsqlParameter("@name", $"Unit {name}"),
            new NpgsqlParameter("@abbreviation", $"u{suffix}"[..8]),
            new NpgsqlParameter("@unitType", unitType));

        return await ExecuteScalarAsync(conn, @"
            INSERT INTO inventory.products (
                company_id, name, sku, barcode, category_id, unit_id, cost_price, sale_price,
                min_stock, max_stock, reorder_point, is_perishable, product_type,
                track_stock, is_for_sale, is_active, created_by
            ) VALUES (
                @companyId, @name, @sku, @barcode, @categoryId, @unitId, 10, @salePrice,
                0, 100, 0, false, @productType, @trackStock, true, true, 1
            ) RETURNING id", new NpgsqlParameter("@companyId", companyId),
            new NpgsqlParameter("@name", name),
            new NpgsqlParameter("@sku", $"SKU-{suffix}"),
            new NpgsqlParameter("@barcode", barcode ?? (object)DBNull.Value),
            new NpgsqlParameter("@categoryId", categoryId),
            new NpgsqlParameter("@unitId", unitId),
            new NpgsqlParameter("@salePrice", salePrice),
            new NpgsqlParameter("@productType", productType),
            new NpgsqlParameter("@trackStock", trackStock));
    }

    private async Task SeedStockAsync(long companyId, long branchId, long productId, decimal quantity)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
            VALUES (@CompanyId, @BranchId, @ProductId, @Quantity)",
            new { CompanyId = companyId, BranchId = branchId, ProductId = productId, Quantity = quantity });
    }

    private static async Task<long> ExecuteScalarAsync(
        System.Data.IDbConnection connection,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)connection);
        cmd.Parameters.AddRange(parameters);
        return (long)(await cmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed failed"));
    }
}
