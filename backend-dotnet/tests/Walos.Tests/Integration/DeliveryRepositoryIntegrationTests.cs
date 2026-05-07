using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class DeliveryRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetOrdersAsync_Should_Return_Only_Orders_For_Requested_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Delivery Co A");
        var companyB = await SeedCompanyAsync("Delivery Co B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB1 = await SeedBranchAsync(companyB, "B-1");

        await SeedDeliveryOrderAsync(companyA, branchA1, "DEL-A1", "new", "Cliente A1");
        await SeedDeliveryOrderAsync(companyA, branchA2, "DEL-A2", "new", "Cliente A2");
        await SeedDeliveryOrderAsync(companyB, branchB1, "DEL-B1", "new", "Cliente B1");

        var orders = (await DeliveryRepository.GetOrdersAsync(companyA, branchA1, null, null, null)).ToList();

        Assert.Single(orders);
        Assert.Equal(companyA, orders[0].CompanyId);
        Assert.Equal(branchA1, orders[0].BranchId);
        Assert.Equal("DEL-A1", orders[0].OrderNumber);
    }

    [SkippableFact]
    public async Task GetOrderByIdAsync_Should_Return_Null_For_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Delivery Read A");
        var companyB = await SeedCompanyAsync("Delivery Read B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");

        var orderId = await SeedDeliveryOrderAsync(companyA, branchA, "DEL-X", "new", "Cliente X");
        await SeedDeliveryStatusHistoryAsync(orderId, null, "new", "Pedido creado");

        var correctRead = await DeliveryRepository.GetOrderByIdAsync(orderId, companyA);
        var wrongRead = await DeliveryRepository.GetOrderByIdAsync(orderId, companyB);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongRead);
    }

    [SkippableFact]
    public async Task UpdateOrderStatusAsync_Should_Not_Update_Order_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Delivery Update A");
        var companyB = await SeedCompanyAsync("Delivery Update B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");

        var orderId = await SeedDeliveryOrderAsync(companyA, branchA, "DEL-UPD", "new", "Cliente Update");
        await SeedDeliveryStatusHistoryAsync(orderId, null, "new", "Pedido creado");

        var wrongCompanyUpdate = await DeliveryRepository.UpdateOrderStatusAsync(
            orderId, companyB, "accepted", "Intento inválido", 1,
            new Dictionary<string, DateTime?> { ["accepted_at"] = DateTime.UtcNow });

        var correctCompanyUpdate = await DeliveryRepository.UpdateOrderStatusAsync(
            orderId, companyA, "accepted", "Pedido aceptado", 1,
            new Dictionary<string, DateTime?> { ["accepted_at"] = DateTime.UtcNow });

        var order = await DeliveryRepository.GetOrderByIdAsync(orderId, companyA);

        Assert.False(wrongCompanyUpdate);
        Assert.True(correctCompanyUpdate);
        Assert.NotNull(order);
        Assert.Equal("accepted", order!.Status);
    }

    private async Task<long> SeedDeliveryOrderAsync(long companyId, long branchId, string orderNumber, string status, string customerName)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO delivery.orders (
                company_id, branch_id, source, order_number, status,
                customer_name, customer_phone, customer_address, notes,
                subtotal, delivery_fee, discount_amount, total, created_by
            )
            VALUES (
                @companyId, @branchId, 'manual', @orderNumber, @status,
                @customerName, '3000000000', 'Calle Test', NULL,
                10000, 0, 0, 10000, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@orderNumber", orderNumber);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@customerName", customerName);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed delivery order"));
    }

    private async Task<long> SeedDeliveryStatusHistoryAsync(long orderId, string? fromStatus, string toStatus, string comment)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO delivery.status_history (order_id, from_status, to_status, comment, changed_by)
            VALUES (@orderId, @fromStatus, @toStatus, @comment, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@orderId", orderId);
        cmd.Parameters.AddWithValue("@fromStatus", fromStatus ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@toStatus", toStatus);
        cmd.Parameters.AddWithValue("@comment", comment);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed delivery history"));
    }
}
