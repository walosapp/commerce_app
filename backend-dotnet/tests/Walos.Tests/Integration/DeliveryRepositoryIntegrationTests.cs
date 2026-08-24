using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.Application.DTOs.Delivery;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class DeliveryRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetOrdersAsync_Returns_Only_Requested_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Delivery list A");
        var companyB = await SeedCompanyAsync("Delivery list B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB = await SeedBranchAsync(companyB, "B-1");

        await SeedDeliveryOrderAsync(companyA, branchA1, "DEL-A1", "new");
        await SeedDeliveryOrderAsync(companyA, branchA2, "DEL-A2", "new");
        await SeedDeliveryOrderAsync(companyB, branchB, "DEL-B1", "new");

        var orders = (await DeliveryRepository.GetOrdersAsync(companyA, branchA1, null, null, null)).ToList();

        var order = Assert.Single(orders);
        Assert.Equal(companyA, order.CompanyId);
        Assert.Equal(branchA1, order.BranchId);
    }

    [SkippableFact]
    public async Task CreateOrder_Uses_Canonical_Product_Name_And_Price()
    {
        var company = await SeedCompanyAsync("Delivery canonical");
        var branch = await SeedBranchAsync(company);
        var product = await SeedProductAsync(company, "Nombre real", 125.50m);

        var created = await Service().CreateOrderAsync(company, branch, 1, Request(
            product,
            quantity: 1.25m,
            clientName: "Nombre manipulado",
            clientPrice: 1m));

        var item = Assert.Single(created.Items!);
        Assert.Equal("Nombre real", item.ProductName);
        Assert.Equal(125.50m, item.UnitPrice);
        Assert.Equal(1.25m, item.Quantity);
        Assert.Equal(156.88m, created.Subtotal);
        Assert.Equal(156.88m, created.Total);
    }

    [SkippableFact]
    public async Task CreateOrder_Rejects_Zero_And_Negative_Quantity_Without_Writes()
    {
        var company = await SeedCompanyAsync("Delivery quantity");
        var branch = await SeedBranchAsync(company);
        var product = await SeedProductAsync(company, "Producto", 10m);
        var service = Service();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(company, branch, 1, Request(product, 0m)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(company, branch, 1, Request(product, -1m)));

        Assert.Equal(0, await CountOrdersAsync(company));
    }

    [SkippableFact]
    public async Task CreateOrder_Rejects_Product_From_Another_Tenant_Without_Writes()
    {
        var companyA = await SeedCompanyAsync("Delivery product tenant A");
        var companyB = await SeedCompanyAsync("Delivery product tenant B");
        var branchA = await SeedBranchAsync(companyA);
        var foreignProduct = await SeedProductAsync(companyB, "Producto B", 30m);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service().CreateOrderAsync(companyA, branchA, 1, Request(foreignProduct, 1m)));

        Assert.Equal(0, await CountOrdersAsync(companyA));
    }

    [SkippableFact]
    public async Task CreateOrder_Rejects_Inactive_Or_Not_For_Sale_Product()
    {
        var company = await SeedCompanyAsync("Delivery unavailable product");
        var branch = await SeedBranchAsync(company);
        var inactive = await SeedProductAsync(company, "Inactivo", 20m, isActive: false);
        var notForSale = await SeedProductAsync(company, "No venta", 20m, isForSale: false);
        var service = Service();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(company, branch, 1, Request(inactive, 1m)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(company, branch, 1, Request(notForSale, 1m)));

        Assert.Equal(0, await CountOrdersAsync(company));
    }

    [SkippableFact]
    public async Task CreateOrder_Rejects_Foreign_And_Inactive_Branch()
    {
        var companyA = await SeedCompanyAsync("Delivery branch tenant A");
        var companyB = await SeedCompanyAsync("Delivery branch tenant B");
        var foreignBranch = await SeedBranchAsync(companyB, "Foreign");
        var inactiveBranch = await SeedBranchAsync(companyA, "Inactive");
        await SetBranchActiveAsync(inactiveBranch, false);
        var product = await SeedProductAsync(companyA, "Producto A", 40m);
        var service = Service();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(companyA, foreignBranch, 1, Request(product, 1m)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(companyA, inactiveBranch, 1, Request(product, 1m)));

        Assert.Equal(0, await CountOrdersAsync(companyA));
    }

    [SkippableFact]
    public async Task Detail_And_Status_Update_Reject_Cross_Branch_Access()
    {
        var company = await SeedCompanyAsync("Delivery branch IDOR");
        var branchA = await SeedBranchAsync(company, "Branch A");
        var branchB = await SeedBranchAsync(company, "Branch B");
        var orderId = await SeedDeliveryOrderAsync(company, branchA, "DEL-IDOR", "new");
        await SeedDeliveryStatusHistoryAsync(orderId, null, "new");

        var foreignRead = await DeliveryRepository.GetOrderByIdAsync(orderId, company, branchB);
        var foreignUpdate = await DeliveryRepository.UpdateOrderStatusAsync(
            orderId, company, branchB, "new", "accepted", null, 1,
            new Dictionary<string, DateTime?> { ["accepted_at"] = DateTime.UtcNow });

        Assert.Null(foreignRead);
        Assert.False(foreignUpdate);
        Assert.Equal("new", await GetOrderStatusAsync(orderId));
        Assert.Equal(1, await CountHistoryAsync(orderId));
    }

    [SkippableFact]
    public async Task Concurrent_Transitions_Commit_Exactly_One_State_And_History_Row()
    {
        var company = await SeedCompanyAsync("Delivery transition race");
        var branch = await SeedBranchAsync(company);
        var orderId = await SeedDeliveryOrderAsync(company, branch, "DEL-RACE", "new");
        await SeedDeliveryStatusHistoryAsync(orderId, null, "new");

        var accept = DeliveryRepository.UpdateOrderStatusAsync(
            orderId, company, branch, "new", "accepted", null, 1,
            new Dictionary<string, DateTime?> { ["accepted_at"] = DateTime.UtcNow });
        var reject = DeliveryRepository.UpdateOrderStatusAsync(
            orderId, company, branch, "new", "rejected", "No disponible", 1,
            new Dictionary<string, DateTime?> { ["rejected_reason"] = null });

        var results = await Task.WhenAll(accept, reject);

        Assert.Single(results.Where(result => result));
        Assert.Equal(2, await CountHistoryAsync(orderId));
        Assert.Contains(await GetOrderStatusAsync(orderId), new[] { "accepted", "rejected" });
    }

    [SkippableFact]
    public async Task CreateOrder_Rolls_Back_Order_When_Item_Insert_Fails()
    {
        var company = await SeedCompanyAsync("Delivery rollback");
        var branch = await SeedBranchAsync(company);
        var product = await SeedProductAsync(company, "Producto rollback", 15m);
        var order = new DeliveryOrder
        {
            CompanyId = company,
            BranchId = branch,
            Source = "manual",
            DeliveryFee = 0,
            DiscountAmount = 0,
            CreatedBy = 1
        };
        var items = new List<DeliveryOrderItem>
        {
            new() { ProductId = product, Quantity = 1, Notes = new string('x', 501) }
        };

        await Assert.ThrowsAsync<PostgresException>(() => DeliveryRepository.CreateOrderAsync(order, items));

        Assert.Equal(0, await CountOrdersAsync(company));
    }

    [SkippableFact]
    public async Task CreateOrder_Rejects_Invalid_Source_And_Excessive_Discount()
    {
        var company = await SeedCompanyAsync("Delivery values");
        var branch = await SeedBranchAsync(company);
        var product = await SeedProductAsync(company, "Producto", 10m);
        var service = Service();
        var invalidSource = Request(product, 1m);
        invalidSource.Source = "unknown";
        var invalidDiscount = Request(product, 1m);
        invalidDiscount.DiscountAmount = 11m;

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(company, branch, 1, invalidSource));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateOrderAsync(company, branch, 1, invalidDiscount));

        Assert.Equal(0, await CountOrdersAsync(company));
    }

    private DeliveryService Service() => new(DeliveryRepository, NullLogger<DeliveryService>.Instance);

    private static CreateDeliveryOrderRequest Request(
        long productId,
        decimal quantity,
        string clientName = "Cliente controla nombre",
        decimal clientPrice = 999m) => new()
    {
        CustomerName = "Cliente",
        Source = "manual",
        Items =
        [
            new DeliveryOrderItemRequest
            {
                ProductId = productId,
                ProductName = clientName,
                Quantity = quantity,
                UnitPrice = clientPrice
            }
        ]
    };

    private async Task<long> SeedProductAsync(
        long companyId,
        string name,
        decimal salePrice,
        bool isActive = true,
        bool isForSale = true)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var category = await ScalarAsync(conn, @"
            INSERT INTO inventory.categories (company_id, name, code, is_active, created_by)
            VALUES (@company, @name, @code, TRUE, 1)
            RETURNING id",
            ("@company", companyId), ("@name", $"Category {name}"), ("@code", $"C{suffix}"));
        var unit = await ScalarAsync(conn, @"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active, created_by)
            VALUES (@company, @name, @abbr, 'weight', TRUE, 1)
            RETURNING id",
            ("@company", companyId), ("@name", $"Unit {name}"), ("@abbr", $"u{suffix}"[..8]));
        return await ScalarAsync(conn, @"
            INSERT INTO inventory.products (
                company_id, name, sku, category_id, unit_id, cost_price, sale_price,
                product_type, track_stock, is_for_sale, is_active, created_by
            ) VALUES (
                @company, @name, @sku, @category, @unit, 5, @price,
                'weighted', FALSE, @forSale, @active, 1
            ) RETURNING id",
            ("@company", companyId), ("@name", name), ("@sku", $"SKU-{suffix}"),
            ("@category", category), ("@unit", unit), ("@price", salePrice),
            ("@forSale", isForSale), ("@active", isActive));
    }

    private async Task<long> SeedDeliveryOrderAsync(
        long companyId,
        long branchId,
        string orderNumber,
        string status)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        return await ScalarAsync(conn, @"
            INSERT INTO delivery.orders (
                company_id, branch_id, source, order_number, status,
                customer_name, subtotal, delivery_fee, discount_amount, total, created_by
            ) VALUES (
                @company, @branch, 'manual', @number, @status,
                'Cliente', 10, 0, 0, 10, 1
            ) RETURNING id",
            ("@company", companyId), ("@branch", branchId),
            ("@number", orderNumber), ("@status", status));
    }

    private async Task SeedDeliveryStatusHistoryAsync(long orderId, string? fromStatus, string toStatus)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO delivery.status_history (order_id, from_status, to_status, comment, changed_by)
            VALUES (@order, @from, @to, 'Test', 1)", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@order", orderId);
        cmd.Parameters.AddWithValue("@from", fromStatus ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@to", toStatus);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetBranchActiveAsync(long branchId, bool active)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "UPDATE core.branches SET is_active=@active WHERE id=@id",
            (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@active", active);
        cmd.Parameters.AddWithValue("@id", branchId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int> CountOrdersAsync(long companyId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM delivery.orders WHERE company_id=@company",
            (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@company", companyId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> CountHistoryAsync(long orderId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM delivery.status_history WHERE order_id=@order",
            (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@order", orderId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<string> GetOrderStatusAsync(long orderId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT status FROM delivery.orders WHERE id=@order",
            (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@order", orderId);
        return (string)(await cmd.ExecuteScalarAsync() ?? throw new InvalidOperationException());
    }

    private static async Task<long> ScalarAsync(
        System.Data.IDbConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = new NpgsqlCommand(sql, (NpgsqlConnection)connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed failed"));
    }
}
