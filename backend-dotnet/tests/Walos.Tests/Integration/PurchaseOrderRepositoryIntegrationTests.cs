using Npgsql;

namespace Walos.Tests.Integration;

public class PurchaseOrderRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetAllAsync_Should_Return_Only_Orders_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("PO Co A");
        var companyB = await SeedCompanyAsync("PO Co B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchB = await SeedBranchAsync(companyB, "B");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var supplierB = await SeedSupplierAsync(companyB, branchB, "Proveedor B");

        await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-A", "pending");
        await SeedPurchaseOrderAsync(companyB, branchB, supplierB, "PO-B", "pending");

        var ordersA = (await PurchaseOrderRepository.GetAllAsync(companyA)).ToList();

        Assert.Single(ordersA);
        Assert.Equal(companyA, ordersA[0].CompanyId);
        Assert.Equal("PO-A", ordersA[0].OrderNumber);
        Assert.DoesNotContain(ordersA, o => o.OrderNumber == "PO-B");
    }

    [SkippableFact]
    public async Task GetByIdAsync_Should_Return_Null_For_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("PO Read A");
        var companyB = await SeedCompanyAsync("PO Read B");
        var branchA = await SeedBranchAsync(companyA, "A");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var orderId = await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-READ", "pending");

        var correctRead = await PurchaseOrderRepository.GetByIdAsync(orderId, companyA);
        var wrongRead = await PurchaseOrderRepository.GetByIdAsync(orderId, companyB);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongRead);
    }

    [SkippableFact]
    public async Task CancelAsync_Should_Not_Cancel_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("PO Cancel A");
        var companyB = await SeedCompanyAsync("PO Cancel B");
        var branchA = await SeedBranchAsync(companyA, "A");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var orderId = await SeedPurchaseOrderAsync(companyA, branchA, supplierA, "PO-CANCEL", "pending");

        var wrongCancel = await PurchaseOrderRepository.CancelAsync(orderId, companyB);
        var correctCancel = await PurchaseOrderRepository.CancelAsync(orderId, companyA);
        var order = await PurchaseOrderRepository.GetByIdAsync(orderId, companyA);

        Assert.False(wrongCancel);
        Assert.True(correctCancel);
        Assert.NotNull(order);
        Assert.Equal("cancelled", order!.Status);
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
                10000, 0, 10000, 1
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
}
