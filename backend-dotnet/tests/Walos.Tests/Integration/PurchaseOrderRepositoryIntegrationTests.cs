using Dapper;
using Npgsql;
using Walos.Application.DTOs.Suppliers;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class PurchaseOrderRepositoryIntegrationTests : IntegrationTestBase
{
    private readonly List<long> _companyIds = [];

    [SkippableFact]
    public async Task CreateAsync_Uses_Server_Loaded_Product_And_Validates_Same_Tenant_References()
    {
        var company = await SeedCompanyAsync("PO Create Safe");
        var branch = await SeedBranchAsync(company, "Main");
        var user = await SeedUserAsync(company, branch, $"{Guid.NewGuid():N}@test.com");
        var supplier = await SeedSupplierAsync(company, branch, "Proveedor canonico");
        var product = await SeedProductAsync(company, "Producto canonico");

        var created = await PurchaseOrderRepository.CreateAsync(company, branch, user,
            new CreatePurchaseOrderRequest
            {
                SupplierId = supplier,
                BranchId = -999,
                Items =
                [
                    new PurchaseOrderItemDto
                    {
                        ProductId = product,
                        ProductName = "Nombre manipulado",
                        Quantity = 2m,
                        UnitCost = 1200m
                    }
                ]
            });

        Assert.Equal(branch, created.BranchId);
        Assert.Equal("Proveedor canonico", created.SupplierName);
        Assert.Equal("Producto canonico", Assert.Single(created.Items).ProductName);
    }

    [SkippableFact]
    public async Task CreateAsync_Rejects_Foreign_Supplier_Without_Writes()
    {
        var companyA = await SeedCompanyAsync("PO Create Supplier A");
        var companyB = await SeedCompanyAsync("PO Create Supplier B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchB = await SeedBranchAsync(companyB, "B");
        var userA = await SeedUserAsync(companyA, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierB = await SeedSupplierAsync(companyB, branchB, "Proveedor B");
        var productA = await SeedProductAsync(companyA, "Producto A");

        await Assert.ThrowsAsync<NotFoundException>(() => PurchaseOrderRepository.CreateAsync(
            companyA, branchA, userA,
            new CreatePurchaseOrderRequest
            {
                SupplierId = supplierB,
                Items = [new PurchaseOrderItemDto { ProductId = productA, Quantity = 1, UnitCost = 10 }]
            }));

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM suppliers.purchase_orders WHERE company_id = @CompanyId",
            new { CompanyId = companyA }));
    }

    [SkippableFact]
    public async Task CreateAsync_Rejects_CrossBranch_Supplier_And_Accepts_CompanyWide_Supplier()
    {
        var company = await SeedCompanyAsync("PO Supplier Branch Scope");
        var branchA = await SeedBranchAsync(company, "A");
        var branchB = await SeedBranchAsync(company, "B");
        var userA = await SeedUserAsync(company, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierB = await SeedSupplierAsync(company, branchB, "Proveedor B");
        var globalSupplier = await SeedSupplierAsync(company, null, "Proveedor Global");
        var product = await SeedProductAsync(company, "Producto");

        await Assert.ThrowsAsync<NotFoundException>(() => PurchaseOrderRepository.CreateAsync(
            company, branchA, userA,
            new CreatePurchaseOrderRequest
            {
                SupplierId = supplierB,
                Items = [new PurchaseOrderItemDto { ProductId = product, Quantity = 1, UnitCost = 10 }]
            }));

        using (var verifyRejected = await ConnectionFactory.CreateConnectionAsync())
        {
            Assert.Equal(0, await verifyRejected.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM suppliers.purchase_orders WHERE company_id = @CompanyId",
                new { CompanyId = company }));
        }

        var created = await PurchaseOrderRepository.CreateAsync(
            company, branchA, userA,
            new CreatePurchaseOrderRequest
            {
                SupplierId = globalSupplier,
                Items = [new PurchaseOrderItemDto { ProductId = product, Quantity = 1, UnitCost = 10 }]
            });

        Assert.Equal(globalSupplier, created.SupplierId);
        Assert.Equal("Proveedor Global", created.SupplierName);
    }

    [SkippableFact]
    public async Task CreateAsync_Rejects_Inactive_Supplier_Without_Writes()
    {
        var company = await SeedCompanyAsync("PO Create Inactive Supplier");
        var branch = await SeedBranchAsync(company, "Main");
        var user = await SeedUserAsync(company, branch, $"{Guid.NewGuid():N}@test.com");
        var supplier = await SeedSupplierAsync(company, branch, "Proveedor inactivo");
        var product = await SeedProductAsync(company, "Producto");
        using (var conn = await ConnectionFactory.CreateConnectionAsync())
            await conn.ExecuteAsync("UPDATE suppliers.suppliers SET is_active = FALSE WHERE id = @Id", new { Id = supplier });

        await Assert.ThrowsAsync<NotFoundException>(() => PurchaseOrderRepository.CreateAsync(
            company, branch, user,
            new CreatePurchaseOrderRequest
            {
                SupplierId = supplier,
                Items = [new PurchaseOrderItemDto { ProductId = product, Quantity = 1, UnitCost = 10 }]
            }));

        using var verify = await ConnectionFactory.CreateConnectionAsync();
        Assert.Equal(0, await verify.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM suppliers.purchase_orders WHERE company_id = @CompanyId",
            new { CompanyId = company }));
    }

    [SkippableFact]
    public async Task CreateAsync_Rejects_Foreign_Or_Inactive_Product_Without_Writes()
    {
        var companyA = await SeedCompanyAsync("PO Create Product A");
        var companyB = await SeedCompanyAsync("PO Create Product B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var userA = await SeedUserAsync(companyA, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var productB = await SeedProductAsync(companyB, "Producto B");
        var inactiveA = await SeedProductAsync(companyA, "Producto inactivo");
        using (var conn = await ConnectionFactory.CreateConnectionAsync())
            await conn.ExecuteAsync("UPDATE inventory.products SET is_active = FALSE WHERE id = @Id", new { Id = inactiveA });

        foreach (var invalidProduct in new[] { productB, inactiveA })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => PurchaseOrderRepository.CreateAsync(
                companyA, branchA, userA,
                new CreatePurchaseOrderRequest
                {
                    SupplierId = supplierA,
                    Items = [new PurchaseOrderItemDto { ProductId = invalidProduct, Quantity = 1, UnitCost = 10 }]
                }));
        }

        using var verify = await ConnectionFactory.CreateConnectionAsync();
        Assert.Equal(0, await verify.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM suppliers.purchase_orders WHERE company_id = @CompanyId",
            new { CompanyId = companyA }));
    }

    [SkippableFact]
    public async Task CreateAsync_Rejects_Foreign_Or_Inactive_Branch()
    {
        var companyA = await SeedCompanyAsync("PO Create Branch A");
        var companyB = await SeedCompanyAsync("PO Create Branch B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchB = await SeedBranchAsync(companyB, "B");
        var inactiveA = await SeedBranchAsync(companyA, "Inactive");
        var userA = await SeedUserAsync(companyA, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var productA = await SeedProductAsync(companyA, "Producto A");
        using (var conn = await ConnectionFactory.CreateConnectionAsync())
            await conn.ExecuteAsync("UPDATE core.branches SET is_active = FALSE WHERE id = @Id", new { Id = inactiveA });

        foreach (var invalidBranch in new[] { branchB, inactiveA })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => PurchaseOrderRepository.CreateAsync(
                companyA, invalidBranch, userA,
                new CreatePurchaseOrderRequest
                {
                    SupplierId = supplierA,
                    Items = [new PurchaseOrderItemDto { ProductId = productA, Quantity = 1, UnitCost = 10 }]
                }));
        }
    }

    [SkippableFact]
    public async Task GetAllAsync_Should_Return_Only_Orders_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("PO Co A");
        var companyB = await SeedCompanyAsync("PO Co B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");
        var branchB = await SeedBranchAsync(companyB, "B");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var supplierA2 = await SeedSupplierAsync(companyA, branchA2, "Proveedor A2");
        var supplierB = await SeedSupplierAsync(companyB, branchB, "Proveedor B");

        await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-A", "pending");
        await SeedPurchaseOrderAsync(companyA, branchA2, supplierA2, "PO-A2", "pending");
        await SeedPurchaseOrderAsync(companyB, branchB, supplierB, "PO-B", "pending");

        var ordersA = (await PurchaseOrderRepository.GetAllAsync(companyA, branchA)).ToList();
        var companyWideOrdersA = (await PurchaseOrderRepository.GetAllAsync(companyA, null)).ToList();

        Assert.Single(ordersA);
        Assert.Equal(companyA, ordersA[0].CompanyId);
        Assert.Equal("PO-A", ordersA[0].OrderNumber);
        Assert.DoesNotContain(ordersA, o => o.OrderNumber == "PO-B");
        Assert.DoesNotContain(ordersA, o => o.OrderNumber == "PO-A2");
        Assert.Equal(2, companyWideOrdersA.Count);
    }

    [SkippableFact]
    public async Task Reads_Should_Hide_Order_With_CrossBranch_Supplier_And_Expose_CompanyWide_Supplier()
    {
        var company = await SeedCompanyAsync("PO Read Supplier Scope");
        var branchA = await SeedBranchAsync(company, "A");
        var branchB = await SeedBranchAsync(company, "B");
        var supplierB = await SeedSupplierAsync(company, branchB, "Proveedor B Privado");
        var globalSupplier = await SeedSupplierAsync(company, null, "Proveedor Global");
        var invalidOrder = await SeedPurchaseOrderAsync(company, branchA, supplierB, "PO-INVALID-SUPPLIER", "pending");
        var globalOrder = await SeedPurchaseOrderAsync(company, branchA, globalSupplier, "PO-GLOBAL-SUPPLIER", "pending");

        var orders = (await PurchaseOrderRepository.GetAllAsync(company, branchA)).ToList();
        var invalidDetail = await PurchaseOrderRepository.GetByIdAsync(invalidOrder, company, branchA);
        var globalDetail = await PurchaseOrderRepository.GetByIdAsync(globalOrder, company, branchA);

        Assert.DoesNotContain(orders, order => order.Id == invalidOrder);
        Assert.DoesNotContain(orders, order => order.SupplierName == "Proveedor B Privado");
        Assert.Contains(orders, order => order.Id == globalOrder);
        Assert.Null(invalidDetail);
        Assert.NotNull(globalDetail);
        Assert.Equal("Proveedor Global", globalDetail!.SupplierName);
    }

    [SkippableFact]
    public async Task GetByIdAsync_Should_Return_Null_For_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("PO Read A");
        var companyB = await SeedCompanyAsync("PO Read B");
        var branchA = await SeedBranchAsync(companyA, "A");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var orderId = await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-READ", "pending");

        var branchA2 = await SeedBranchAsync(companyA, "A2");
        var correctRead = await PurchaseOrderRepository.GetByIdAsync(orderId, companyA, branchA);
        var wrongCompanyRead = await PurchaseOrderRepository.GetByIdAsync(orderId, companyB, branchA);
        var wrongBranchRead = await PurchaseOrderRepository.GetByIdAsync(orderId, companyA, branchA2);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongCompanyRead);
        Assert.Null(wrongBranchRead);
    }

    [SkippableFact]
    public async Task CancelAsync_Should_Not_Cancel_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("PO Cancel A");
        var companyB = await SeedCompanyAsync("PO Cancel B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var orderId = await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-CANCEL", "pending");

        var wrongCompanyCancel = await PurchaseOrderRepository.CancelAsync(orderId, companyB, branchA);
        var wrongBranchCancel = await PurchaseOrderRepository.CancelAsync(orderId, companyA, branchA2);
        var correctCancel = await PurchaseOrderRepository.CancelAsync(orderId, companyA, branchA);
        var order = await PurchaseOrderRepository.GetByIdAsync(orderId, companyA, branchA);

        Assert.False(wrongCompanyCancel);
        Assert.False(wrongBranchCancel);
        Assert.True(correctCancel);
        Assert.NotNull(order);
        Assert.Equal("cancelled", order!.Status);
    }

    [SkippableFact]
    public async Task ReceiveAsync_Should_Accept_CompanyWide_Supplier_And_Create_Movement()
    {
        var companyId = await SeedCompanyAsync("PO Receive Normal");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var userId = await SeedUserAsync(companyId, branchId, $"{Guid.NewGuid():N}@test.com");
        var supplierId = await SeedSupplierAsync(companyId, null, "Proveedor Global");
        var productId = await SeedProductAsync(companyId, "Producto Normal");
        var orderId = await SeedPurchaseOrderAsync(companyId, branchId, supplierId, "PO-NORMAL", "pending");
        var itemId = await SeedPurchaseOrderItemAsync(orderId, productId, "Producto Normal", 5m, 2500m);

        var result = await PurchaseOrderRepository.ReceiveAsync(
            orderId, companyId, branchId, userId,
            new ReceivePurchaseOrderRequest
            {
                Notes = "Recepcion normal",
                Items = [new ReceiveItemDto { OrderItemId = itemId, ReceivedQty = 3m }]
            });

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var stock = await conn.QuerySingleAsync<decimal>(@"
            SELECT quantity FROM inventory.stock
            WHERE company_id = @CompanyId AND branch_id = @BranchId AND product_id = @ProductId",
            new { CompanyId = companyId, BranchId = branchId, ProductId = productId });
        var movement = await conn.QuerySingleAsync<MovementSnapshot>(@"
            SELECT movement_type AS MovementType, quantity AS Quantity,
                   unit_cost AS UnitCost, reference_type AS ReferenceType,
                   reference_id AS ReferenceId, stock_after AS StockAfter
            FROM inventory.movements
            WHERE company_id = @CompanyId AND reference_type = 'purchase_order' AND reference_id = @OrderId",
            new { CompanyId = companyId, OrderId = orderId });

        Assert.Equal("received", result.Status);
        Assert.Equal(3m, stock);
        Assert.Equal("purchase", movement.MovementType);
        Assert.Equal(3m, movement.Quantity);
        Assert.Equal(2500m, movement.UnitCost);
        Assert.Equal("purchase_order", movement.ReferenceType);
        Assert.Equal(orderId, movement.ReferenceId);
        Assert.Equal(3m, movement.StockAfter);
    }

    [SkippableFact]
    public async Task ReceiveAsync_Should_Reject_Product_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("PO Product Tenant A");
        var companyB = await SeedCompanyAsync("PO Product Tenant B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var userA = await SeedUserAsync(companyA, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var productB = await SeedProductAsync(companyB, "Producto B");
        var orderA = await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-PRODUCT-IDOR", "pending");
        var itemId = await SeedPurchaseOrderItemAsync(orderA, productB, "Producto B", 2m, 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PurchaseOrderRepository.ReceiveAsync(
                orderA, companyA, branchA, userA,
                new ReceivePurchaseOrderRequest
                {
                    Items = [new ReceiveItemDto { OrderItemId = itemId, ReceivedQty = 2m }]
                }));

        await AssertOrderWasNotModifiedAsync(orderA, itemId, companyA);
    }

    [SkippableFact]
    public async Task ReceiveAsync_Should_Reject_Item_From_Another_Order()
    {
        var companyId = await SeedCompanyAsync("PO Item Isolation");
        var branchId = await SeedBranchAsync(companyId, "Main");
        var userId = await SeedUserAsync(companyId, branchId, $"{Guid.NewGuid():N}@test.com");
        var supplierId = await SeedSupplierAsync(companyId, branchId, "Proveedor");
        var productId = await SeedProductAsync(companyId, "Producto");
        var orderA = await SeedPurchaseOrderAsync(companyId, branchId, supplierId, "PO-ITEM-A", "pending");
        var orderB = await SeedPurchaseOrderAsync(companyId, branchId, supplierId, "PO-ITEM-B", "pending");
        var itemA = await SeedPurchaseOrderItemAsync(orderA, productId, "Producto", 2m, 1000m);
        var itemB = await SeedPurchaseOrderItemAsync(orderB, productId, "Producto", 4m, 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PurchaseOrderRepository.ReceiveAsync(
                orderA, companyId, branchId, userId,
                new ReceivePurchaseOrderRequest
                {
                    Items = [new ReceiveItemDto { OrderItemId = itemB, ReceivedQty = 1m }]
                }));

        await AssertOrderWasNotModifiedAsync(orderA, itemA, companyId);
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        Assert.Null(await conn.ExecuteScalarAsync<decimal?>(
            "SELECT received_qty FROM suppliers.purchase_order_items WHERE id = @Id", new { Id = itemB }));
    }

    [SkippableFact]
    public async Task ReceiveAsync_Should_Reject_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("PO Order Tenant A");
        var companyB = await SeedCompanyAsync("PO Order Tenant B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchB = await SeedBranchAsync(companyB, "B");
        var userA = await SeedUserAsync(companyA, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierB = await SeedSupplierAsync(companyB, branchB, "Proveedor B");
        var productB = await SeedProductAsync(companyB, "Producto B");
        var orderB = await SeedPurchaseOrderAsync(companyB, branchB, supplierB, "PO-ORDER-IDOR", "pending");
        var itemB = await SeedPurchaseOrderItemAsync(orderB, productB, "Producto B", 2m, 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PurchaseOrderRepository.ReceiveAsync(
                orderB, companyA, branchA, userA,
                new ReceivePurchaseOrderRequest
                {
                    Items = [new ReceiveItemDto { OrderItemId = itemB, ReceivedQty = 2m }]
                }));

        await AssertOrderWasNotModifiedAsync(orderB, itemB, companyB);
    }

    [SkippableFact]
    public async Task ReceiveAsync_Should_Reject_Historical_Order_With_CrossBranch_Supplier_Without_Writes()
    {
        var company = await SeedCompanyAsync("PO Receive Supplier Scope");
        var branchA = await SeedBranchAsync(company, "A");
        var branchB = await SeedBranchAsync(company, "B");
        var userA = await SeedUserAsync(company, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierB = await SeedSupplierAsync(company, branchB, "Proveedor B");
        var product = await SeedProductAsync(company, "Producto");
        var order = await SeedPurchaseOrderAsync(company, branchA, supplierB, "PO-HISTORICAL-INVALID", "pending");
        var item = await SeedPurchaseOrderItemAsync(order, product, "Producto", 2m, 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PurchaseOrderRepository.ReceiveAsync(
                order, company, branchA, userA,
                new ReceivePurchaseOrderRequest
                {
                    Items = [new ReceiveItemDto { OrderItemId = item, ReceivedQty = 2m }]
                }));

        await AssertOrderWasNotModifiedAsync(order, item, company);
        using var verify = await ConnectionFactory.CreateConnectionAsync();
        Assert.Equal(0, await verify.QuerySingleAsync<int>(@"
            SELECT COUNT(*)::int
            FROM inventory.stock
            WHERE company_id = @CompanyId AND branch_id = @BranchId AND product_id = @ProductId",
            new { CompanyId = company, BranchId = branchA, ProductId = product }));
    }

    [SkippableFact]
    public async Task ReceiveAsync_Should_Roll_Back_Item_When_Stock_Update_Fails()
    {
        var companyA = await SeedCompanyAsync("PO Rollback A");
        var companyB = await SeedCompanyAsync("PO Rollback B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var userA = await SeedUserAsync(companyA, branchA, $"{Guid.NewGuid():N}@test.com");
        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var productA = await SeedProductAsync(companyA, "Producto A");
        var orderA = await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-ROLLBACK", "pending");
        var itemA = await SeedPurchaseOrderItemAsync(orderA, productA, "Producto A", 2m, 1000m);

        using (var conn = await ConnectionFactory.CreateConnectionAsync())
        {
            await conn.ExecuteAsync(@"
                INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
                VALUES (@WrongCompanyId, @BranchId, @ProductId, 5)",
                new { WrongCompanyId = companyB, BranchId = branchA, ProductId = productA });
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PurchaseOrderRepository.ReceiveAsync(
                orderA, companyA, branchA, userA,
                new ReceivePurchaseOrderRequest
                {
                    Items = [new ReceiveItemDto { OrderItemId = itemA, ReceivedQty = 2m }]
                }));

        await AssertOrderWasNotModifiedAsync(orderA, itemA, companyA);
        using var verify = await ConnectionFactory.CreateConnectionAsync();
        Assert.Equal(5m, await verify.QuerySingleAsync<decimal>(@"
            SELECT quantity FROM inventory.stock WHERE branch_id = @BranchId AND product_id = @ProductId",
            new { BranchId = branchA, ProductId = productA }));
    }

    private new async Task<long> SeedCompanyAsync(string name = "Test Company")
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var companyId = await conn.QuerySingleAsync<long>(@"
            INSERT INTO core.companies
                (name, legal_name, tax_id, email, phone, is_active, created_by)
            VALUES
                (@Name, @Name, @TaxId, @Email, '123456', true, 1)
            RETURNING id",
            new
            {
                Name = name,
                TaxId = $"TEST-{suffix}",
                Email = $"{suffix}@test.com"
            });
        _companyIds.Add(companyId);
        return companyId;
    }

    private new async Task<long> SeedBranchAsync(long companyId, string name = "Test Branch")
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        return await conn.QuerySingleAsync<long>(@"
            INSERT INTO core.branches
                (company_id, name, code, branch_type, address, city, is_active, created_by)
            VALUES
                (@CompanyId, @Name, @Code, 'store', 'Test Address', 'Test City', true, 1)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                Name = name,
                Code = $"T{Guid.NewGuid().ToString("N")[..10]}"
            });
    }

    private async Task<long> SeedSupplierAsync(long companyId, long? branchId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO suppliers.suppliers (
                company_id, branch_id, name, contact_name, phone, email, address, notes, is_active, created_by
            )
            VALUES (
                @companyId, @branchId, @name, 'Contacto', '3000000000', @email, 'Dir', NULL, true, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", $"{Guid.NewGuid():N}@test.com");
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed supplier"));
    }

    private async Task<long> SeedPurchaseOrderAsync(long companyId, long branchId, long supplierId, string orderNumber, string status)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO suppliers.purchase_orders (
                company_id, branch_id, supplier_id, order_number, status, notes, expected_date,
                subtotal, tax, total, created_by
            )
            VALUES (
                @companyId, @branchId, @supplierId, @orderNumber, @status, NULL, NOW(),
                10000, 0, 10000, NULL
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@supplierId", supplierId);
        cmd.Parameters.AddWithValue("@orderNumber", orderNumber);
        cmd.Parameters.AddWithValue("@status", status);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed purchase order"));
    }

    private async Task<long> SeedProductAsync(long companyId, string name)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var categoryId = await conn.QuerySingleAsync<long>(@"
            INSERT INTO inventory.categories (company_id, name, code)
            VALUES (@CompanyId, @Name, @Code)
            RETURNING id",
            new { CompanyId = companyId, Name = $"Category {suffix}", Code = $"C{suffix}" });
        var unitId = await conn.QuerySingleAsync<long>(@"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type)
            VALUES (@CompanyId, @Name, @Abbreviation, 'unit')
            RETURNING id",
            new { CompanyId = companyId, Name = $"Unit {suffix}", Abbreviation = $"U{suffix[..6]}" });

        return await conn.QuerySingleAsync<long>(@"
            INSERT INTO inventory.products
                (company_id, name, sku, category_id, unit_id, cost_price, sale_price)
            VALUES
                (@CompanyId, @Name, @Sku, @CategoryId, @UnitId, 1000, 1500)
            RETURNING id",
            new { CompanyId = companyId, Name = name, Sku = $"SKU-{suffix}", CategoryId = categoryId, UnitId = unitId });
    }

    private async Task<long> SeedPurchaseOrderItemAsync(
        long orderId, long productId, string productName, decimal quantity, decimal unitCost)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        return await conn.QuerySingleAsync<long>(@"
            INSERT INTO suppliers.purchase_order_items
                (order_id, product_id, product_name, quantity, unit_cost, subtotal)
            VALUES
                (@OrderId, @ProductId, @ProductName, @Quantity, @UnitCost, @Subtotal)
            RETURNING id",
            new
            {
                OrderId = orderId,
                ProductId = productId,
                ProductName = productName,
                Quantity = quantity,
                UnitCost = unitCost,
                Subtotal = quantity * unitCost
            });
    }

    private async Task AssertOrderWasNotModifiedAsync(long orderId, long itemId, long companyId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        Assert.Equal("pending", await conn.QuerySingleAsync<string>(
            "SELECT status FROM suppliers.purchase_orders WHERE id = @Id", new { Id = orderId }));
        Assert.Null(await conn.ExecuteScalarAsync<decimal?>(
            "SELECT received_qty FROM suppliers.purchase_order_items WHERE id = @Id", new { Id = itemId }));
        Assert.Equal(0, await conn.QuerySingleAsync<int>(@"
            SELECT COUNT(*)::int FROM inventory.movements
            WHERE company_id = @CompanyId AND reference_type = 'purchase_order' AND reference_id = @OrderId",
            new { CompanyId = companyId, OrderId = orderId }));
    }

    public override void Dispose()
    {
        try
        {
            if (_companyIds.Count > 0)
            {
                using var conn = ConnectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
                conn.Execute(@"
                    DELETE FROM suppliers.purchase_order_items
                    WHERE order_id IN (
                        SELECT id FROM suppliers.purchase_orders WHERE company_id = ANY(@CompanyIds)
                    );
                    DELETE FROM suppliers.purchase_orders WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM suppliers.supplier_products
                    WHERE supplier_id IN (
                        SELECT id FROM suppliers.suppliers WHERE company_id = ANY(@CompanyIds)
                    );
                    DELETE FROM suppliers.suppliers WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM inventory.movements WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM inventory.stock
                    WHERE company_id = ANY(@CompanyIds)
                       OR branch_id IN (SELECT id FROM core.branches WHERE company_id = ANY(@CompanyIds))
                       OR product_id IN (SELECT id FROM inventory.products WHERE company_id = ANY(@CompanyIds));
                    DELETE FROM inventory.products WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM inventory.categories WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM inventory.units WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM finance.entries WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM finance.categories WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM core.users WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM core.branches WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM core.roles WHERE company_id = ANY(@CompanyIds);
                    DELETE FROM core.companies WHERE id = ANY(@CompanyIds);",
                    new { CompanyIds = _companyIds.ToArray() });
            }
        }
        finally
        {
            base.Dispose();
        }
    }

    private sealed class MovementSnapshot
    {
        public string MovementType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public long ReferenceId { get; set; }
        public decimal StockAfter { get; set; }
    }
}
