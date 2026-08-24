using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Integration;

public class SalesServiceIntegrationTests : IntegrationTestBase
{
    [SkippableTheory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(999999)]
    public async Task CreateTableAsync_Should_Persist_Canonical_Product_Snapshots(int submittedPrice)
    {
        var companyId = await SeedCompanyAsync($"Canonical Sales {submittedPrice}");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var productId = await SeedProductAsync(
            companyId, branchId, "Producto canonico", 125.50m, "simple", true, true, false);
        var service = CreateService();

        var result = await service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
        {
            Items =
            [
                new CreateTableItemDto
                {
                    ProductId = productId,
                    ProductName = "Nombre manipulado",
                    Quantity = 2,
                    UnitPrice = submittedPrice
                }
            ]
        });

        var order = await SalesRepository.GetOrderByTableIdAsync(result.Table.Id, companyId);
        var item = Assert.Single(await SalesRepository.GetOrderItemsAsync(order!.Id, companyId));

        Assert.Equal("Producto canonico", item.ProductName);
        Assert.Equal(125.50m, item.UnitPrice);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(251m, item.Subtotal);
        Assert.Equal(251m, order.Subtotal);
        Assert.Equal(251m, result.Total);
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateTableAsync_Should_Reject_NonPositive_Quantity_Without_Writes(int quantity)
    {
        var companyId = await SeedCompanyAsync($"Invalid Quantity {quantity}");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var productId = await SeedProductAsync(
            companyId, branchId, "Producto", 50m, "simple", true, true, false);
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = productId, Quantity = quantity }]
            }));

        Assert.Equal(new DatabaseCounts(0, 0, 0), await GetSalesCountsAsync(companyId));
    }

    [SkippableFact]
    public async Task CreateTableAsync_Should_Accept_Positive_Decimal_Quantity_For_Weighted_Product()
    {
        var companyId = await SeedCompanyAsync("Weighted Sales");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var productId = await SeedProductAsync(
            companyId, branchId, "Producto pesado", 40m, "weighted", true, true, false);
        var service = CreateService();

        var result = await service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
        {
            Items = [new CreateTableItemDto { ProductId = productId, Quantity = 1.25m }]
        });

        var order = await SalesRepository.GetOrderByTableIdAsync(result.Table.Id, companyId);
        var item = Assert.Single(await SalesRepository.GetOrderItemsAsync(order!.Id, companyId));
        Assert.Equal(1.25m, item.Quantity);
        Assert.Equal(50m, item.Subtotal);
    }

    [SkippableFact]
    public async Task CreateTableAsync_Should_Reject_Product_From_Another_Company_Without_Writes()
    {
        var companyA = await SeedCompanyAsync("Product Owner A");
        var branchA = await SeedBranchAsync(companyA, "A");
        var productA = await SeedProductAsync(
            companyA, branchA, "Privado A", 80m, "simple", true, true, true, 10m);
        var companyB = await SeedCompanyAsync("Sales Actor B");
        var branchB = await SeedBranchAsync(companyB, "B");
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateTableAsync(companyB, branchB, 1, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = productA, Quantity = 1 }]
            }));

        Assert.Equal(new DatabaseCounts(0, 0, 0), await GetSalesCountsAsync(companyB));
        Assert.Equal(10m, await GetStockQuantityAsync(branchA, productA));
    }

    [SkippableFact]
    public async Task CreateTableAsync_Should_Reject_Inactive_Product_Without_Writes()
    {
        var companyId = await SeedCompanyAsync("Inactive Product Sales");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var productId = await SeedProductAsync(
            companyId, branchId, "Inactivo", 30m, "simple", false, true, false);
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = productId, Quantity = 1 }]
            }));

        Assert.Equal(new DatabaseCounts(0, 0, 0), await GetSalesCountsAsync(companyId));
    }

    [SkippableFact]
    public async Task CreateTableAsync_Should_Reject_Product_Not_For_Sale_Without_Writes()
    {
        var companyId = await SeedCompanyAsync("Not For Sale Product");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var productId = await SeedProductAsync(
            companyId, branchId, "No vendible", 30m, "simple", true, false, false);
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = productId, Quantity = 1 }]
            }));

        Assert.Equal(new DatabaseCounts(0, 0, 0), await GetSalesCountsAsync(companyId));
    }

    [SkippableFact]
    public async Task AddItemsToTableAsync_Should_Ignore_Manipulated_Name_And_Price()
    {
        var companyId = await SeedCompanyAsync("Add Items Sales");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var initialProduct = await SeedProductAsync(
            companyId, branchId, "Inicial", 10m, "simple", true, true, false);
        var addedProduct = await SeedProductAsync(
            companyId, branchId, "Agregado canonico", 75.25m, "weighted", true, true, false);
        var service = CreateService();
        var created = await service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
        {
            Items = [new CreateTableItemDto { ProductId = initialProduct, Quantity = 1 }]
        });

        await service.AddItemsToTableAsync(companyId, branchId, created.Table.Id,
        [
            new CreateTableItemDto
            {
                ProductId = addedProduct,
                ProductName = "Falso",
                Quantity = 1.5m,
                UnitPrice = 0.01m
            }
        ]);

        var order = await SalesRepository.GetOrderByTableIdAsync(created.Table.Id, companyId);
        var addedItem = (await SalesRepository.GetOrderItemsAsync(order!.Id, companyId))
            .Single(item => item.ProductId == addedProduct);
        Assert.Equal("Agregado canonico", addedItem.ProductName);
        Assert.Equal(75.25m, addedItem.UnitPrice);
        Assert.Equal(112.88m, addedItem.Subtotal);
    }

    [SkippableFact]
    public async Task Rejected_Mixed_Request_Should_Not_Persist_Partial_Sales_Or_Modify_Stock()
    {
        var companyId = await SeedCompanyAsync("Atomic Validation Sales");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var validProduct = await SeedProductAsync(
            companyId, branchId, "Valido", 20m, "simple", true, true, true, 10m);
        var invalidProduct = await SeedProductAsync(
            companyId, branchId, "Inactivo", 30m, "simple", false, true, false);
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateTableAsync(companyId, branchId, 1, new CreateTableRequest
            {
                Items =
                [
                    new CreateTableItemDto { ProductId = validProduct, Quantity = 2 },
                    new CreateTableItemDto { ProductId = invalidProduct, Quantity = 1 }
                ]
            }));

        Assert.Equal(new DatabaseCounts(0, 0, 0), await GetSalesCountsAsync(companyId));
        Assert.Equal(10m, await GetStockQuantityAsync(branchId, validProduct));
    }

    private SalesService CreateService()
        => new(
            SalesRepository,
            InventoryRepository,
            CompanyRepository,
            Mock.Of<IRecipeRepository>(),
            Mock.Of<ICreditRepository>(),
            Mock.Of<ICashRegisterRepository>(),
            Mock.Of<IOrderPaymentRepository>(),
            Mock.Of<IUsersRepository>(),
            Mock.Of<IRefundRepository>(),
            CheckoutRepository,
            NullLogger<SalesService>.Instance);

    private async Task<long> SeedProductAsync(
        long companyId,
        long branchId,
        string name,
        decimal salePrice,
        string productType,
        bool isActive,
        bool isForSale,
        bool trackStock,
        decimal stockQuantity = 0)
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
            VALUES (@companyId, @name, @abbreviation, 'unit', true, 1)
            RETURNING id", new NpgsqlParameter("@companyId", companyId),
            new NpgsqlParameter("@name", $"Unit {name}"),
            new NpgsqlParameter("@abbreviation", $"u{suffix}"[..8]));
        var productId = await ExecuteScalarAsync(conn, @"
            INSERT INTO inventory.products (
                company_id, name, sku, category_id, unit_id, cost_price, sale_price,
                min_stock, max_stock, reorder_point, is_perishable, product_type,
                track_stock, is_for_sale, is_active, created_by
            ) VALUES (
                @companyId, @name, @sku, @categoryId, @unitId, 10, @salePrice,
                0, 100, 0, false, @productType,
                @trackStock, @isForSale, @isActive, 1
            ) RETURNING id", new NpgsqlParameter("@companyId", companyId),
            new NpgsqlParameter("@name", name),
            new NpgsqlParameter("@sku", $"SKU-{suffix}"),
            new NpgsqlParameter("@categoryId", categoryId),
            new NpgsqlParameter("@unitId", unitId),
            new NpgsqlParameter("@salePrice", salePrice),
            new NpgsqlParameter("@productType", productType),
            new NpgsqlParameter("@trackStock", trackStock),
            new NpgsqlParameter("@isForSale", isForSale),
            new NpgsqlParameter("@isActive", isActive));

        if (trackStock)
        {
            using var stockCmd = new NpgsqlCommand(@"
                INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity, reserved_quantity)
                VALUES (@companyId, @branchId, @productId, @quantity, 0)", (NpgsqlConnection)conn);
            stockCmd.Parameters.AddWithValue("@companyId", companyId);
            stockCmd.Parameters.AddWithValue("@branchId", branchId);
            stockCmd.Parameters.AddWithValue("@productId", productId);
            stockCmd.Parameters.AddWithValue("@quantity", stockQuantity);
            await stockCmd.ExecuteNonQueryAsync();
        }

        return productId;
    }

    private static async Task<long> ExecuteScalarAsync(
        System.Data.IDbConnection connection,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)connection);
        cmd.Parameters.AddRange(parameters);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed sales test data"));
    }

    private async Task<DatabaseCounts> GetSalesCountsAsync(long companyId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT
                (SELECT COUNT(*) FROM sales.tables WHERE company_id = @companyId),
                (SELECT COUNT(*) FROM sales.orders WHERE company_id = @companyId),
                (SELECT COUNT(*) FROM sales.order_items WHERE company_id = @companyId)", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new DatabaseCounts(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private async Task<decimal> GetStockQuantityAsync(long branchId, long productId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT quantity FROM inventory.stock
            WHERE branch_id = @branchId AND product_id = @productId", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@productId", productId);
        return (decimal)(await cmd.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Stock row not found"));
    }

    private sealed record DatabaseCounts(long Tables, long Orders, long Items);
}
