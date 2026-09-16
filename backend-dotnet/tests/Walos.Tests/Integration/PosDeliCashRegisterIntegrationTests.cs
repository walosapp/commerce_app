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
using Walos.Domain.Policies;
using Walos.Infrastructure.Inventory;

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

    private sealed class PersistedMovement
    {
        public decimal Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public string? ReferenceType { get; init; }
        public long? ReferenceId { get; init; }
        public decimal? StockAfter { get; init; }
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

    [SkippableFact]
    public async Task CreateSale_Inactive_Product_Is_Rejected_Without_Writes()
    {
        var context = await SeedPosContextAsync("POS inactive");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto inactivo", 100m);
        await ExecuteAsync("UPDATE inventory.products SET is_active = FALSE WHERE id = @Id", new { Id = productId });

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 100m)));

        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_NotForSale_Product_Is_Rejected_Without_Writes()
    {
        var context = await SeedPosContextAsync("POS not for sale");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto no vendible", 100m);
        await ExecuteAsync("UPDATE inventory.products SET is_for_sale = FALSE WHERE id = @Id", new { Id = productId });

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 100m)));

        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Product_From_Another_Tenant_Is_Rejected_Without_Writes()
    {
        var tenantA = await SeedPosContextAsync("POS tenant A");
        var tenantB = await SeedPosContextAsync("POS tenant B");
        await DisableCashRegisterRequirementAsync(tenantA.Company);
        var foreignProduct = await SeedProductAsync(tenantB.Company, "Producto B", 100m);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateController(tenantA.Company, tenantA.Branch, tenantA.User)
                .CreateSale(SaleRequest(foreignProduct, 1m, 100m)));

        Assert.Equal(0, await CountOrdersAsync(tenantA.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Zero_Quantity_Is_Rejected_Before_Writes()
    {
        var context = await SeedPosContextAsync("POS zero");
        var productId = await SeedProductAsync(context.Company, "Producto cero", 100m);

        var result = await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(productId, 0m, 100m));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Negative_Quantity_Is_Rejected_Before_Writes()
    {
        var context = await SeedPosContextAsync("POS negative");
        var productId = await SeedProductAsync(context.Company, "Producto negativo", 100m);

        var result = await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(productId, -1m, 100m));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Unknown_Payment_Method_Is_Rejected_Instead_Of_Other()
    {
        var context = await SeedPosContextAsync("POS unknown payment");
        var productId = await SeedProductAsync(context.Company, "Producto unknown", 100m);
        var request = SaleRequest(productId, 1m, 100m);
        request.Payments[0].Method = "crypto";

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateController(context.Company, context.Branch, context.User).CreateSale(request));

        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Nequi_Remains_Persisted_But_Is_Accounted_As_Transfer()
    {
        var context = await SeedPosContextAsync("POS nequi");
        var productId = await SeedProductAsync(context.Company, "Producto nequi", 100m);
        var register = await OpenRegisterAsync(context);
        var request = SaleRequest(productId, 1m, 100m);
        request.Payments[0].Method = "NeQuI";

        var result = await CreateController(context.Company, context.Branch, context.User).CreateSale(request);

        Assert.IsType<OkObjectResult>(result);
        var paymentMethod = await ScalarAsync<string>(
            "SELECT method FROM sales.order_payments WHERE company_id = @CompanyId",
            new { CompanyId = context.Company });
        var persistedRegister = await CashRegisterRepository.GetByIdAsync(register.Id, context.Company);
        Assert.Equal("nequi", paymentMethod);
        Assert.NotNull(persistedRegister);
        Assert.Equal(100m, persistedRegister!.TotalTransferSales);
        Assert.Equal(0m, persistedRegister.TotalOtherSales);
    }

    [SkippableFact]
    public async Task CreateSale_Payment_Difference_Of_One_Cent_Is_Accepted()
    {
        var context = await SeedPosContextAsync("POS one cent");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto cent", 100m);

        var result = await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(productId, 1m, 99.99m));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(99.99m, await ScalarAsync<decimal>(
            "SELECT amount FROM sales.order_payments WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
    }

    [SkippableFact]
    public async Task CreateSale_Payment_Difference_Above_One_Cent_Is_Rejected_With_Rollback()
    {
        var context = await SeedPosContextAsync("POS two cents");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto cents", 100m);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 99.98m)));

        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Prepared_Product_Consumes_Recipe_Ingredient()
    {
        var context = await SeedPosContextAsync("POS recipe");
        await DisableCashRegisterRequirementAsync(context.Company);
        var ingredient = await SeedProductAsync(context.Company, "Ingrediente", 5m, trackStock: true);
        var prepared = await SeedProductAsync(
            context.Company, "Preparado", 50m, productType: "prepared", trackStock: false);
        await SeedStockAsync(context.Company, context.Branch, ingredient, 10m);
        await SeedRecipeAsync(context.Company, prepared, ingredient, 2m);

        var result = await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(prepared, 1m, 50m));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<PosDeliSaleResponse>>(ok.Value);
        Assert.Equal(8m, await StockAsync(context, ingredient));
        Assert.Equal(1L, await ScalarAsync<long>(@"
            SELECT COUNT(*) FROM inventory.movements
            WHERE company_id = @CompanyId AND product_id = @ProductId AND movement_type = 'recipe_consumption'",
            new { CompanyId = context.Company, ProductId = ingredient }));
        var movement = await QuerySingleAsync<PersistedMovement>(@"
            SELECT unit_cost AS UnitCost, reference_type AS ReferenceType,
                   reference_id AS ReferenceId, stock_after AS StockAfter
            FROM inventory.movements
            WHERE company_id = @CompanyId AND product_id = @ProductId",
            new { CompanyId = context.Company, ProductId = ingredient });
        Assert.Equal(10m, movement.UnitCost);
        Assert.Equal("order", movement.ReferenceType);
        Assert.Equal(response.Data!.SaleId, movement.ReferenceId);
        Assert.Equal(8m, movement.StockAfter);
    }

    [SkippableFact]
    public async Task CreateSale_Prepared_Without_Recipe_Is_Rejected_Without_Writes()
    {
        var context = await SeedPosContextAsync("POS no recipe");
        await DisableCashRegisterRequirementAsync(context.Company);
        var prepared = await SeedProductAsync(
            context.Company, "Preparado sin receta", 50m, productType: "prepared", trackStock: false);

        await Assert.ThrowsAsync<BusinessException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(prepared, 1m, 50m)));

        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Prepared_With_Inactive_Ingredient_Is_Rejected_Without_Writes()
    {
        var context = await SeedPosContextAsync("POS inactive ingredient");
        await DisableCashRegisterRequirementAsync(context.Company);
        var ingredient = await SeedProductAsync(context.Company, "Ingrediente inactivo", 5m, trackStock: true);
        var prepared = await SeedProductAsync(
            context.Company, "Preparado invalido", 50m, productType: "prepared", trackStock: false);
        await SeedStockAsync(context.Company, context.Branch, ingredient, 10m);
        await SeedRecipeAsync(context.Company, prepared, ingredient, 2m);
        await ExecuteAsync(
            "UPDATE inventory.products SET is_active = FALSE WHERE id = @ProductId",
            new { ProductId = ingredient });

        await Assert.ThrowsAsync<BusinessException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(prepared, 1m, 50m)));

        Assert.Equal(10m, await StockAsync(context, ingredient));
        Assert.Equal(0, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Prepared_Products_Group_Repeated_Ingredient()
    {
        var context = await SeedPosContextAsync("POS grouped recipe");
        await DisableCashRegisterRequirementAsync(context.Company);
        var ingredient = await SeedProductAsync(context.Company, "Ingrediente compartido", 5m, trackStock: true);
        var preparedA = await SeedProductAsync(
            context.Company, "Preparado agrupado A", 50m, productType: "prepared", trackStock: false);
        var preparedB = await SeedProductAsync(
            context.Company, "Preparado agrupado B", 60m, productType: "prepared", trackStock: false);
        await SeedStockAsync(context.Company, context.Branch, ingredient, 10m);
        await SeedRecipeAsync(context.Company, preparedA, ingredient, 2m);
        await SeedRecipeAsync(context.Company, preparedB, ingredient, 3m);

        var result = await CreateController(context.Company, context.Branch, context.User).CreateSale(new PosDeliSaleRequest
        {
            Items =
            [
                new PosDeliSaleItem { ProductId = preparedA, Quantity = 1m },
                new PosDeliSaleItem { ProductId = preparedB, Quantity = 1m }
            ],
            Payments = [new PosDeliPayment { Method = "cash", Amount = 110m }]
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(5m, await StockAsync(context, ingredient));
        var movement = await QuerySingleAsync<PersistedMovement>(@"
            SELECT quantity AS Quantity, stock_after AS StockAfter
            FROM inventory.movements
            WHERE company_id = @CompanyId AND product_id = @ProductId",
            new { CompanyId = context.Company, ProductId = ingredient });
        Assert.Equal(5m, movement.Quantity);
        Assert.Equal(5m, movement.StockAfter);
    }

    [SkippableFact]
    public async Task CreateSale_Movement_Contains_Real_Cost_Reference_And_StockAfter()
    {
        var context = await SeedPosContextAsync("POS movement");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto movement", 100m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 5m);

        var result = await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(productId, 1m, 100m));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<PosDeliSaleResponse>>(ok.Value);
        var movement = await QuerySingleAsync<PersistedMovement>(@"
            SELECT unit_cost AS UnitCost, reference_type AS ReferenceType,
                   reference_id AS ReferenceId, stock_after AS StockAfter
            FROM inventory.movements
            WHERE company_id = @CompanyId AND product_id = @ProductId",
            new { CompanyId = context.Company, ProductId = productId });
        Assert.Equal(10m, movement.UnitCost);
        Assert.Equal("order", movement.ReferenceType);
        Assert.Equal(response.Data!.SaleId, movement.ReferenceId);
        Assert.Equal(4m, movement.StockAfter);
    }

    [SkippableFact]
    public async Task CreateSale_CurrentBehavior_Retry_Creates_A_Second_Sale_Pending_Block_13()
    {
        var context = await SeedPosContextAsync("POS retry");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto retry", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 5m);
        var request = SaleRequest(productId, 1m, 20m);
        var controller = CreateController(context.Company, context.Branch, context.User);

        await controller.CreateSale(request);
        await controller.CreateSale(request);

        Assert.Equal(2, await CountOrdersAsync(context.Company));
        Assert.Equal(3m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task CreateSale_CurrentBehavior_Different_Retry_Payload_Creates_Another_Sale_Pending_Block_13()
    {
        var context = await SeedPosContextAsync("POS payload retry");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto payload", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 5m);
        var controller = CreateController(context.Company, context.Branch, context.User);

        await controller.CreateSale(SaleRequest(productId, 1m, 20m));
        await controller.CreateSale(SaleRequest(productId, 2m, 40m));

        Assert.Equal(2, await CountOrdersAsync(context.Company));
        Assert.Equal(2m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task CreateSale_Same_Key_And_Payload_Replays_Persisted_Sale_Without_Mutations()
    {
        var context = await SeedPosContextAsync("POS idempotent replay");
        var productId = await SeedProductAsync(
            context.Company, "Producto idempotente", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 5m);
        var register = await OpenRegisterAsync(context);
        var request = SaleRequest(productId, 1m, 20m);
        request.CashReceived = 50m;
        var controller = CreateController(context.Company, context.Branch, context.User);
        const string key = "sale-replay-1";

        var first = ExtractSale(await controller.CreateSale(request, key));
        var replay = ExtractSale(await controller.CreateSale(request, key));

        Assert.Equal(first.SaleId, replay.SaleId);
        Assert.Equal(first.TicketNumber, replay.TicketNumber);
        Assert.Equal(first.Total, replay.Total);
        Assert.Equal(first.Change, replay.Change);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM sales.order_items WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM sales.order_payments WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM inventory.movements WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
        Assert.Equal(4m, await StockAsync(context, productId));

        var persistedRegister = await CashRegisterRepository.GetByIdAsync(register.Id, context.Company);
        Assert.NotNull(persistedRegister);
        Assert.Equal(20m, persistedRegister!.TotalSales);
        Assert.Equal(20m, persistedRegister.TotalCashSales);
        Assert.Equal(1, persistedRegister.OrderCount);
        Assert.Equal(64, await ScalarAsync<int>(
            "SELECT LENGTH(request_fingerprint)::INT FROM sales.orders WHERE id = @OrderId",
            new { OrderId = first.SaleId }));
    }

    [SkippableTheory]
    [InlineData("quantity")]
    [InlineData("product")]
    [InlineData("method")]
    [InlineData("amount")]
    public async Task CreateSale_Same_Key_With_Different_Economic_Intent_Returns_Conflict(string change)
    {
        var context = await SeedPosContextAsync($"POS idempotent conflict {change}");
        await DisableCashRegisterRequirementAsync(context.Company);
        var firstProduct = await SeedProductAsync(context.Company, "Producto original", 20m);
        var otherProduct = await SeedProductAsync(context.Company, "Producto alterno", 20m);
        var controller = CreateController(context.Company, context.Branch, context.User);
        const string key = "sale-conflict-1";
        await controller.CreateSale(SaleRequest(firstProduct, 1m, 20m), key);

        var retry = change switch
        {
            "quantity" => SaleRequest(firstProduct, 2m, 40m),
            "product" => SaleRequest(otherProduct, 1m, 20m),
            "method" => SaleRequest(firstProduct, 1m, 20m, "card"),
            "amount" => SaleRequest(firstProduct, 1m, 21m),
            _ => throw new InvalidOperationException("Unknown test case")
        };

        var conflict = Assert.IsType<ConflictObjectResult>(await controller.CreateSale(retry, key));
        var response = Assert.IsType<ApiResponse>(conflict.Value);
        Assert.Equal("idempotency_conflict", response.Code);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Different_Key_With_Same_Payload_Creates_New_Sale()
    {
        var context = await SeedPosContextAsync("POS different idempotency key");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto keys", 20m);
        var request = SaleRequest(productId, 1m, 20m);
        var controller = CreateController(context.Company, context.Branch, context.User);

        var first = ExtractSale(await controller.CreateSale(request, "sale-key-a"));
        var second = ExtractSale(await controller.CreateSale(request, "sale-key-b"));

        Assert.NotEqual(first.SaleId, second.SaleId);
        Assert.Equal(2, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Reordered_Items_And_Transfer_Alias_Replay_Same_Intent()
    {
        var context = await SeedPosContextAsync("POS canonical replay");
        await DisableCashRegisterRequirementAsync(context.Company);
        var firstProduct = await SeedProductAsync(context.Company, "Producto canonical A", 10m);
        var secondProduct = await SeedProductAsync(context.Company, "Producto canonical B", 20m);
        var firstRequest = new PosDeliSaleRequest
        {
            Items =
            [
                new PosDeliSaleItem { ProductId = firstProduct, Quantity = 1m },
                new PosDeliSaleItem { ProductId = secondProduct, Quantity = 1m }
            ],
            Payments = [new PosDeliPayment { Method = "nequi", Amount = 30m, Reference = "REF" }]
        };
        var reordered = new PosDeliSaleRequest
        {
            Items =
            [
                new PosDeliSaleItem { ProductId = secondProduct, Quantity = 1m },
                new PosDeliSaleItem { ProductId = firstProduct, Quantity = 1m }
            ],
            Payments = [new PosDeliPayment { Method = "transfer", Amount = 30m, Reference = "REF" }]
        };
        var controller = CreateController(context.Company, context.Branch, context.User);

        var first = ExtractSale(await controller.CreateSale(firstRequest, "sale-canonical"));
        var replay = ExtractSale(await controller.CreateSale(reordered, "sale-canonical"));

        Assert.Equal(first.SaleId, replay.SaleId);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Same_Key_Is_Isolated_By_Company_And_Scoped_Across_Branches()
    {
        var tenantA = await SeedPosContextAsync("POS idempotency tenant A");
        var tenantB = await SeedPosContextAsync("POS idempotency tenant B");
        await DisableCashRegisterRequirementAsync(tenantA.Company);
        await DisableCashRegisterRequirementAsync(tenantB.Company);
        var productA = await SeedProductAsync(tenantA.Company, "Producto tenant A", 20m);
        var productB = await SeedProductAsync(tenantB.Company, "Producto tenant B", 20m);
        const string key = "shared-company-key";

        var saleA = ExtractSale(await CreateController(tenantA.Company, tenantA.Branch, tenantA.User)
            .CreateSale(SaleRequest(productA, 1m, 20m), key));
        var saleB = ExtractSale(await CreateController(tenantB.Company, tenantB.Branch, tenantB.User)
            .CreateSale(SaleRequest(productB, 1m, 20m), key));

        Assert.NotEqual(saleA.SaleId, saleB.SaleId);

        var otherBranch = await SeedBranchAsync(tenantA.Company, "POS idempotency other branch");
        var otherUser = await SeedUserAsync(
            tenantA.Company,
            otherBranch,
            $"idempotency-{Guid.NewGuid():N}@test.com");
        var conflict = Assert.IsType<ConflictObjectResult>(await CreateController(
                tenantA.Company, otherBranch, otherUser)
            .CreateSale(SaleRequest(productA, 1m, 20m), key));
        Assert.Equal("idempotency_conflict", Assert.IsType<ApiResponse>(conflict.Value).Code);
    }

    [SkippableFact]
    public async Task CreateSale_Failed_Inventory_Does_Not_Consume_Key()
    {
        var context = await SeedPosContextAsync("POS idempotency rollback");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(
            context.Company, "Producto idempotency rollback", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 0.5m);
        var controller = CreateController(context.Company, context.Branch, context.User);
        var request = SaleRequest(productId, 1m, 20m);
        const string key = "sale-rollback";

        await Assert.ThrowsAsync<BusinessException>(() => controller.CreateSale(request, key));
        await ExecuteAsync(@"
            UPDATE inventory.stock SET quantity = 2
            WHERE company_id = @CompanyId AND branch_id = @BranchId AND product_id = @ProductId",
            new { CompanyId = context.Company, BranchId = context.Branch, ProductId = productId });

        var completed = ExtractSale(await controller.CreateSale(request, key));

        Assert.True(completed.SaleId > 0);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
        Assert.Equal(1m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task CreateSale_Concurrent_Same_Key_Mutates_Exactly_Once()
    {
        var context = await SeedPosContextAsync("POS idempotent concurrent");
        var productId = await SeedProductAsync(
            context.Company, "Producto idempotent concurrent", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 2m);
        var register = await OpenRegisterAsync(context);
        var request = SaleRequest(productId, 1m, 20m);
        const string key = "sale-concurrent";

        var results = await RunBehindIdempotencyGateAsync(
            context.Company,
            key,
            () => CreateController(context.Company, context.Branch, context.User).CreateSale(request, key),
            () => CreateController(context.Company, context.Branch, context.User).CreateSale(request, key));
        var sales = results.Select(ExtractSale).ToList();

        Assert.Equal(sales[0].SaleId, sales[1].SaleId);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
        Assert.Equal(1m, await StockAsync(context, productId));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM inventory.movements WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM sales.order_payments WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
        var persistedRegister = await CashRegisterRepository.GetByIdAsync(register.Id, context.Company);
        Assert.NotNull(persistedRegister);
        Assert.Equal(20m, persistedRegister!.TotalSales);
        Assert.Equal(1, persistedRegister.OrderCount);
    }

    [SkippableFact]
    public async Task CreateSale_Concurrent_Sales_Do_Not_Persist_Negative_Stock()
    {
        var context = await SeedPosContextAsync("POS concurrent");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto concurrent", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 1m);
        var outcomes = await RunBehindPaymentGateAsync(context.Company,
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 20m)),
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 20m)));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is BusinessException);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
        Assert.Equal(0m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task CreateSale_Two_Concurrent_Pos_Sales_Consume_Exact_Stock()
    {
        var context = await SeedPosContextAsync("POS concurrent exact");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto exacto", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 2m);

        var outcomes = await RunBehindPaymentGateAsync(context.Company,
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 20m)),
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 20m)));

        Assert.All(outcomes, outcome => Assert.Null(outcome));
        Assert.Equal(2, await CountOrdersAsync(context.Company));
        Assert.Equal(0m, await StockAsync(context, productId));
        Assert.Equal(2L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM inventory.movements WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
    }

    [SkippableFact]
    public async Task CreateSale_Two_Prepared_Products_Serialize_Shared_Ingredient()
    {
        var context = await SeedPosContextAsync("POS prepared concurrent");
        await DisableCashRegisterRequirementAsync(context.Company);
        var ingredient = await SeedProductAsync(context.Company, "Ingrediente escaso", 5m, trackStock: true);
        var preparedA = await SeedProductAsync(
            context.Company, "Preparado A", 50m, productType: "prepared", trackStock: false);
        var preparedB = await SeedProductAsync(
            context.Company, "Preparado B", 60m, productType: "prepared", trackStock: false);
        await SeedStockAsync(context.Company, context.Branch, ingredient, 3m);
        await SeedRecipeAsync(context.Company, preparedA, ingredient, 2m);
        await SeedRecipeAsync(context.Company, preparedB, ingredient, 2m);

        var outcomes = await RunBehindPaymentGateAsync(context.Company,
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(preparedA, 1m, 50m)),
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(preparedB, 1m, 60m)));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is BusinessException);
        Assert.Equal(1m, await StockAsync(context, ingredient));
        Assert.Equal(1L, await ScalarAsync<long>(@"
            SELECT COUNT(*) FROM inventory.movements
            WHERE company_id = @CompanyId AND product_id = @ProductId",
            new { CompanyId = context.Company, ProductId = ingredient }));
    }

    [SkippableFact]
    public async Task CreateSale_Deterministic_Product_Locks_Avoid_Deadlock()
    {
        var context = await SeedPosContextAsync("POS deterministic locks");
        await DisableCashRegisterRequirementAsync(context.Company);
        var first = await SeedProductAsync(context.Company, "Producto lock A", 10m, trackStock: true);
        var second = await SeedProductAsync(context.Company, "Producto lock B", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, first, 2m);
        await SeedStockAsync(context.Company, context.Branch, second, 2m);

        PosDeliSaleRequest Request(long left, long right) => new()
        {
            Items =
            [
                new PosDeliSaleItem { ProductId = left, Quantity = 1m },
                new PosDeliSaleItem { ProductId = right, Quantity = 1m }
            ],
            Payments = [new PosDeliPayment { Method = "cash", Amount = 30m }]
        };

        var outcomes = await RunBehindPaymentGateAsync(context.Company,
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(Request(first, second)),
            () => CreateController(context.Company, context.Branch, context.User)
                .CreateSale(Request(second, first)));

        Assert.All(outcomes, outcome => Assert.Null(outcome));
        Assert.Equal(0m, await StockAsync(context, first));
        Assert.Equal(0m, await StockAsync(context, second));
    }

    [SkippableFact]
    public async Task CreateSale_Movement_Insert_Failure_Rolls_Back_Stock_And_Sale()
    {
        var context = await SeedPosContextAsync("POS movement rollback");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto rollback movement", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 2m);
        var suffix = Guid.NewGuid().ToString("N");
        var functionName = $"fail_pos_movement_{suffix}";
        var triggerName = $"trg_fail_pos_movement_{suffix}";
        var controller = CreateController(context.Company, context.Branch, context.User);
        var request = SaleRequest(productId, 1m, 20m);
        const string key = "sale-movement-rollback";
        await ExecuteAsync($@"
            CREATE FUNCTION {functionName}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id = {context.Company} THEN RAISE EXCEPTION 'forced movement failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {triggerName} BEFORE INSERT ON inventory.movements
            FOR EACH ROW EXECUTE FUNCTION {functionName}();");

        try
        {
            await Assert.ThrowsAsync<PostgresException>(() =>
                controller.CreateSale(request, key));
            Assert.Equal(2m, await StockAsync(context, productId));
            Assert.Equal(0, await CountOrdersAsync(context.Company));
        }
        finally
        {
            await ExecuteAsync($@"
                DROP TRIGGER IF EXISTS {triggerName} ON inventory.movements;
                DROP FUNCTION IF EXISTS {functionName}();");
        }

        var completed = ExtractSale(await controller.CreateSale(request, key));
        Assert.True(completed.SaleId > 0);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
        Assert.Equal(1m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task CreateSale_Payment_Insert_Failure_Does_Not_Consume_Key()
    {
        var context = await SeedPosContextAsync("POS payment rollback key");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto payment rollback", 20m);
        var suffix = Guid.NewGuid().ToString("N");
        var functionName = $"fail_pos_payment_{suffix}";
        var triggerName = $"trg_fail_pos_payment_{suffix}";
        var controller = CreateController(context.Company, context.Branch, context.User);
        var request = SaleRequest(productId, 1m, 20m);
        const string key = "sale-payment-rollback";
        await ExecuteAsync($@"
            CREATE FUNCTION {functionName}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id = {context.Company} THEN RAISE EXCEPTION 'forced payment failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {triggerName} BEFORE INSERT ON sales.order_payments
            FOR EACH ROW EXECUTE FUNCTION {functionName}();");

        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => controller.CreateSale(request, key));
            Assert.Equal(0, await CountOrdersAsync(context.Company));
        }
        finally
        {
            await ExecuteAsync($@"
                DROP TRIGGER IF EXISTS {triggerName} ON sales.order_payments;
                DROP FUNCTION IF EXISTS {functionName}();");
        }

        var completed = ExtractSale(await controller.CreateSale(request, key));
        Assert.True(completed.SaleId > 0);
        Assert.Equal(1, await CountOrdersAsync(context.Company));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM sales.order_payments WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
    }

    [SkippableFact]
    public async Task CreateSale_Cash_Update_Failure_Does_Not_Consume_Key_Or_Inventory()
    {
        var context = await SeedPosContextAsync("POS cash rollback key");
        var productId = await SeedProductAsync(
            context.Company, "Producto cash rollback", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 2m);
        var register = await OpenRegisterAsync(context);
        var suffix = Guid.NewGuid().ToString("N");
        var functionName = $"fail_pos_cash_{suffix}";
        var triggerName = $"trg_fail_pos_cash_{suffix}";
        var controller = CreateController(context.Company, context.Branch, context.User);
        var request = SaleRequest(productId, 1m, 20m);
        const string key = "sale-cash-rollback";
        await ExecuteAsync($@"
            CREATE FUNCTION {functionName}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id = {context.Company} THEN RAISE EXCEPTION 'forced cash failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {triggerName} BEFORE UPDATE ON sales.cash_registers
            FOR EACH ROW EXECUTE FUNCTION {functionName}();");

        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => controller.CreateSale(request, key));
            Assert.Equal(0, await CountOrdersAsync(context.Company));
            Assert.Equal(2m, await StockAsync(context, productId));
        }
        finally
        {
            await ExecuteAsync($@"
                DROP TRIGGER IF EXISTS {triggerName} ON sales.cash_registers;
                DROP FUNCTION IF EXISTS {functionName}();");
        }

        var completed = ExtractSale(await controller.CreateSale(request, key));
        Assert.True(completed.SaleId > 0);
        Assert.Equal(1m, await StockAsync(context, productId));
        var persistedRegister = await CashRegisterRepository.GetByIdAsync(register.Id, context.Company);
        Assert.NotNull(persistedRegister);
        Assert.Equal(20m, persistedRegister!.TotalSales);
        Assert.Equal(1, persistedRegister.OrderCount);
    }

    [SkippableFact]
    public async Task CreateSale_Insufficient_Stock_Rolls_Back_All_Sale_Writes()
    {
        var context = await SeedPosContextAsync("POS insufficient");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto insufficient", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 0.5m);

        await Assert.ThrowsAsync<BusinessException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 20m)));

        Assert.Equal(0, await CountOrdersAsync(context.Company));
        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM inventory.movements WHERE company_id = @CompanyId",
            new { CompanyId = context.Company }));
        Assert.Equal(0.5m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task CreateSale_Respects_Stock_Committed_By_Open_Table()
    {
        var context = await SeedPosContextAsync("POS committed");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto committed", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 1m);
        await SeedPendingRestaurantOrderAsync(context, productId, 1m, 20m);

        await Assert.ThrowsAsync<BusinessException>(() =>
            CreateController(context.Company, context.Branch, context.User)
                .CreateSale(SaleRequest(productId, 1m, 20m)));

        Assert.Equal(1m, await StockAsync(context, productId));
        Assert.Equal(0L, await ScalarAsync<long>(@"
            SELECT COUNT(*) FROM sales.orders
            WHERE company_id = @CompanyId AND status = 'completed'",
            new { CompanyId = context.Company }));
        Assert.Equal(1L, await ScalarAsync<long>(@"
            SELECT COUNT(*) FROM sales.orders
            WHERE company_id = @CompanyId AND status = 'pending'",
            new { CompanyId = context.Company }));
    }

    [SkippableFact]
    public async Task Restaurant_And_Pos_Competing_For_Reserved_Stock_Preserve_Restaurant_Order()
    {
        var context = await SeedPosContextAsync("Restaurant POS competition");
        await DisableCashRegisterRequirementAsync(context.Company);
        var productId = await SeedProductAsync(context.Company, "Producto compartido", 20m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 1m);
        var restaurant = await SeedPendingRestaurantOrderAsync(context, productId, 1m, 20m);

        async Task<Exception?> Capture(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var checkout = Capture(async () => await CheckoutRepository.ProcessAsync(new CheckoutCommand
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            UserId = context.User,
            TableId = restaurant.TableId,
            Payments = [new CheckoutPayment("cash", 20m, null)]
        }));
        var pos = Capture(async () => await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(productId, 1m, 20m)));

        var outcomes = await Task.WhenAll(checkout, pos);

        Assert.Null(outcomes[0]);
        Assert.IsType<BusinessException>(outcomes[1]);
        Assert.Equal(0m, await StockAsync(context, productId));
        Assert.Equal("completed", await ScalarAsync<string>(
            "SELECT status FROM sales.orders WHERE id = @OrderId",
            new { restaurant.OrderId }));
        Assert.Equal(1, await CountOrdersAsync(context.Company));
    }

    [SkippableFact]
    public async Task CreateSale_Quantity_With_More_Than_Two_Decimals_Is_Temporarily_Rejected()
    {
        var context = await SeedPosContextAsync("POS precision");
        var productId = await SeedProductAsync(context.Company, "Producto precision", 100m, trackStock: true);
        await SeedStockAsync(context.Company, context.Branch, productId, 10m);

        var result = await CreateController(context.Company, context.Branch, context.User)
            .CreateSale(SaleRequest(productId, 1.255m, 125.50m));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await CountOrdersAsync(context.Company));
        Assert.Equal(10m, await StockAsync(context, productId));
    }

    [SkippableFact]
    public async Task Database_Currently_Rounds_OrderItem_Quantity_1255_To_126()
    {
        var context = await SeedPosContextAsync("DB precision");
        var productId = await SeedProductAsync(context.Company, "Producto db precision", 100m);
        var tableId = await ScalarAsync<long>(@"
            INSERT INTO sales.tables (company_id, branch_id, table_number, name, status, created_by)
            VALUES (@CompanyId, @BranchId, 800001, 'Precision', 'open', @UserId)
            RETURNING id", new { CompanyId = context.Company, BranchId = context.Branch, UserId = context.User });
        var orderId = await ScalarAsync<long>(@"
            INSERT INTO sales.orders (
                company_id, branch_id, table_id, order_number, status,
                subtotal, tax, total, created_by)
            VALUES (@CompanyId, @BranchId, @TableId, 'PRECISION', 'pending', 125.50, 0, 125.50, @UserId)
            RETURNING id", new
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            TableId = tableId,
            UserId = context.User
        });

        await ExecuteAsync(@"
            INSERT INTO sales.order_items (
                company_id, order_id, product_id, product_name, quantity, unit_price)
            VALUES (@CompanyId, @OrderId, @ProductId, 'Precision', 1.255, 100)", new
        {
            CompanyId = context.Company,
            OrderId = orderId,
            ProductId = productId
        });

        var item = await QuerySingleAsync<PersistedSaleItem>(@"
            SELECT quantity AS Quantity, unit_price AS UnitPrice, subtotal AS Subtotal
            FROM sales.order_items WHERE order_id = @OrderId", new { OrderId = orderId });
        Assert.Equal(1.26m, item.Quantity);
        Assert.Equal(126m, item.Subtotal);
        Assert.Equal(125.50m, await ScalarAsync<decimal>(
            "SELECT total FROM sales.orders WHERE id = @OrderId", new { OrderId = orderId }));
    }

    private static PosDeliSaleRequest SaleRequest(
        long productId,
        decimal quantity,
        decimal paymentAmount,
        string paymentMethod = "cash") => new()
    {
        Items = [new PosDeliSaleItem { ProductId = productId, Quantity = quantity }],
        Payments = [new PosDeliPayment { Method = paymentMethod, Amount = paymentAmount }]
    };

    private static PosDeliSaleResponse ExtractSale(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<ApiResponse<PosDeliSaleResponse>>(ok.Value).Data!;
    }

    private async Task<CashRegister> OpenRegisterAsync((long Company, long Branch, long User) context) =>
        await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            OpenedBy = context.User,
            Status = "open",
            OpeningAmount = 0,
            OpenedAt = DateTime.UtcNow
        });

    private Task DisableCashRegisterRequirementAsync(long companyId) =>
        ExecuteAsync(
            "UPDATE core.companies SET require_cash_register = FALSE WHERE id = @CompanyId",
            new { CompanyId = companyId });

    private async Task<int> CountOrdersAsync(long companyId) =>
        await ScalarAsync<int>(
            "SELECT COUNT(*)::INT FROM sales.orders WHERE company_id = @CompanyId",
            new { CompanyId = companyId });

    private async Task<decimal> StockAsync(
        (long Company, long Branch, long User) context,
        long productId) =>
        await StockAsync(context.Company, context.Branch, productId);

    private async Task<decimal> StockAsync(long companyId, long branchId, long productId) =>
        await ScalarAsync<decimal>(@"
            SELECT quantity FROM inventory.stock
            WHERE company_id = @CompanyId AND branch_id = @BranchId AND product_id = @ProductId",
            new { CompanyId = companyId, BranchId = branchId, ProductId = productId });

    private async Task SeedRecipeAsync(long companyId, long productId, long ingredientId, decimal quantity)
    {
        var unitId = await ScalarAsync<long>(
            "SELECT unit_id FROM inventory.products WHERE id = @ProductId AND company_id = @CompanyId",
            new { ProductId = ingredientId, CompanyId = companyId });
        await ExecuteAsync(@"
            INSERT INTO inventory.recipes (
                company_id, product_id, ingredient_id, quantity, unit_id)
            VALUES (@CompanyId, @ProductId, @IngredientId, @Quantity, @UnitId)", new
        {
            CompanyId = companyId,
            ProductId = productId,
            IngredientId = ingredientId,
            Quantity = quantity,
            UnitId = unitId
        });
    }

    private async Task<(long TableId, long OrderId)> SeedPendingRestaurantOrderAsync(
        (long Company, long Branch, long User) context,
        long productId,
        decimal quantity,
        decimal unitPrice)
    {
        var tableId = await ScalarAsync<long>(@"
            INSERT INTO sales.tables (
                company_id, branch_id, table_number, name, status, created_by)
            VALUES (@CompanyId, @BranchId, 700001, 'Pendiente', 'open', @UserId)
            RETURNING id", new
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            UserId = context.User
        });
        var orderId = await ScalarAsync<long>(@"
            INSERT INTO sales.orders (
                company_id, branch_id, table_id, order_number, status,
                subtotal, tax, total, created_by)
            VALUES (
                @CompanyId, @BranchId, @TableId, 'PENDING-POS', 'pending',
                @Total, 0, @Total, @UserId)
            RETURNING id", new
        {
            CompanyId = context.Company,
            BranchId = context.Branch,
            TableId = tableId,
            Total = quantity * unitPrice,
            UserId = context.User
        });
        await ExecuteAsync(@"
            INSERT INTO sales.order_items (
                company_id, order_id, product_id, product_name, quantity, unit_price)
            VALUES (@CompanyId, @OrderId, @ProductId, 'Comprometido', @Quantity, @UnitPrice)", new
        {
            CompanyId = context.Company,
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        });

        return (tableId, orderId);
    }

    private async Task<Exception?[]> RunBehindPaymentGateAsync(
        long companyId,
        params Func<Task<IActionResult>>[] actions)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var functionName = $"gate_pos_payment_{suffix}";
        var triggerName = $"gate_pos_payment_trigger_{suffix}";
        var gateKey = Random.Shared.NextInt64(1_000_000_000L, long.MaxValue);
        await ExecuteAsync($@"
            CREATE FUNCTION {functionName}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id = {companyId} THEN PERFORM pg_advisory_xact_lock({gateKey}); END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {triggerName}
            BEFORE INSERT ON sales.order_payments
            FOR EACH ROW EXECUTE FUNCTION {functionName}();");

        Task<Exception?> Capture(Func<Task<IActionResult>> action) => Task.Run(async () =>
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });

        var tasks = Array.Empty<Task<Exception?>>();
        await using var gateConnection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await using var gateTransaction = await gateConnection.BeginTransactionAsync();
        await new NpgsqlCommand($"SELECT pg_advisory_xact_lock({gateKey})", gateConnection, gateTransaction)
            .ExecuteNonQueryAsync();
        var gateReleased = false;

        try
        {
            tasks = actions.Select(Capture).ToArray();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var waiters = await ScalarAsync<int>(@"
                    SELECT COUNT(*)::INT
                    FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND wait_event = 'advisory'");
                if (waiters >= actions.Length)
                    break;

                await Task.Delay(20);
            }

            var blocked = await ScalarAsync<int>(@"
                SELECT COUNT(*)::INT
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND wait_event = 'advisory'");
            Assert.True(blocked >= actions.Length, "Las ventas no alcanzaron la barrera transaccional");

            await gateTransaction.CommitAsync();
            gateReleased = true;
            return await Task.WhenAll(tasks);
        }
        finally
        {
            if (!gateReleased)
                await gateTransaction.RollbackAsync();
            if (tasks.Length > 0)
                await Task.WhenAll(tasks);
            await ExecuteAsync($@"
                DROP TRIGGER IF EXISTS {triggerName} ON sales.order_payments;
                DROP FUNCTION IF EXISTS {functionName}();");
        }
    }

    private async Task<IActionResult[]> RunBehindIdempotencyGateAsync(
        long companyId,
        string idempotencyKey,
        params Func<Task<IActionResult>>[] actions)
    {
        var lockKey = PosSaleIdempotencyPolicy.CreateAdvisoryLockKey(companyId, idempotencyKey);
        await using var gateConnection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await using var gateTransaction = await gateConnection.BeginTransactionAsync();
        await new NpgsqlCommand($"SELECT pg_advisory_xact_lock({lockKey})", gateConnection, gateTransaction)
            .ExecuteNonQueryAsync();

        var tasks = actions.Select(action => Task.Run(action)).ToArray();
        var gateReleased = false;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var waiters = await ScalarAsync<int>(@"
                    SELECT COUNT(*)::INT
                    FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND wait_event = 'advisory'");
                if (waiters >= actions.Length)
                    break;

                await Task.Delay(20);
            }

            var blocked = await ScalarAsync<int>(@"
                SELECT COUNT(*)::INT
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND wait_event = 'advisory'");
            Assert.True(blocked >= actions.Length, "Las ventas no alcanzaron la barrera de idempotencia");

            await gateTransaction.CommitAsync();
            gateReleased = true;
            return await Task.WhenAll(tasks);
        }
        finally
        {
            if (!gateReleased)
                await gateTransaction.RollbackAsync();
            await Task.WhenAll(tasks);
        }
    }

    private async Task ExecuteAsync(string sql, object? parameters = null)
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(sql, parameters);
    }

    private async Task<T> ScalarAsync<T>(string sql, object? parameters = null)
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        return (await connection.ExecuteScalarAsync<T>(sql, parameters))!;
    }

    private async Task<T> QuerySingleAsync<T>(string sql, object? parameters = null)
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        return await connection.QuerySingleAsync<T>(sql, parameters);
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
        NullLogger<PosDeliController>.Instance,
        new SaleInventoryPlanBuilder(),
        new InventoryTransactionWriter());

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
