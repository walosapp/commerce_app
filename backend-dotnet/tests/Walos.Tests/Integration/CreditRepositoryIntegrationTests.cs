using Npgsql;

namespace Walos.Tests.Integration;

public class CreditRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetCreditsAsync_Should_Return_Only_Credits_For_Requested_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Credit Co A");
        var companyB = await SeedCompanyAsync("Credit Co B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");
        var branchB = await SeedBranchAsync(companyB, "B");

        await SeedCreditAsync(companyA, branchA, "Cliente A", "CR-A", "pending", 20000m, 5000m);
        await SeedCreditAsync(companyA, branchA2, "Cliente A2", "CR-A2", "pending", 22000m, 5000m);
        await SeedCreditAsync(companyB, branchB, "Cliente B", "CR-B", "pending", 30000m, 10000m);

        var creditsA = (await CreditRepository.GetCreditsAsync(companyA, branchA, "pending", null)).ToList();

        Assert.Single(creditsA);
        Assert.Equal(companyA, creditsA[0].CompanyId);
        Assert.Equal("CR-A", creditsA[0].OrderNumber);
        Assert.DoesNotContain(creditsA, c => c.OrderNumber == "CR-A2");
        Assert.DoesNotContain(creditsA, c => c.OrderNumber == "CR-B");
    }

    [SkippableFact]
    public async Task GetCreditByIdAsync_Should_Return_Null_For_Credit_From_Another_Company_Or_Branch()
    {
        var companyA = await SeedCompanyAsync("Credit Read A");
        var companyB = await SeedCompanyAsync("Credit Read B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");

        var creditId = await SeedCreditAsync(companyA, branchA, "Cliente A", "CR-READ", "pending", 25000m, 10000m);

        var correctRead = await CreditRepository.GetCreditByIdAsync(creditId, companyA, branchA);
        var wrongCompanyRead = await CreditRepository.GetCreditByIdAsync(creditId, companyB, branchA);
        var wrongBranchRead = await CreditRepository.GetCreditByIdAsync(creditId, companyA, branchA2);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongCompanyRead);
        Assert.Null(wrongBranchRead);
    }

    [SkippableFact]
    public async Task GetCreditByOrderAsync_Should_Use_Order_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Credit Order A");
        var companyB = await SeedCompanyAsync("Credit Order B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");
        var branchB = await SeedBranchAsync(companyB, "B");
        var orderId = await SeedOrderAsync(companyA, branchA, "CR-ORDER");
        await SeedCreditAsync(companyA, branchA, "Cliente orden", "CR-ORDER", "pending", 25000m, 10000m, orderId);

        var correctRead = await CreditRepository.GetCreditByOrderAsync(orderId, companyA, branchA);
        var wrongCompanyRead = await CreditRepository.GetCreditByOrderAsync(orderId, companyB, branchB);
        var wrongBranchRead = await CreditRepository.GetCreditByOrderAsync(orderId, companyA, branchA2);

        Assert.NotNull(correctRead);
        Assert.Equal(orderId, correctRead!.OrderId);
        Assert.Equal(15000m, correctRead.CreditAmount);
        Assert.Null(wrongCompanyRead);
        Assert.Null(wrongBranchRead);
    }

    [SkippableFact]
    public async Task CancelCreditAsync_Should_Not_Cancel_Credit_From_Another_Company_Or_Branch()
    {
        var companyA = await SeedCompanyAsync("Credit Cancel A");
        var companyB = await SeedCompanyAsync("Credit Cancel B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");

        var creditId = await SeedCreditAsync(companyA, branchA, "Cliente A", "CR-CANCEL", "pending", 18000m, 6000m);

        Assert.False(await CreditRepository.CancelCreditAsync(creditId, companyB, branchA));
        Assert.False(await CreditRepository.CancelCreditAsync(creditId, companyA, branchA2));
        var afterWrongCancel = await CreditRepository.GetCreditByIdAsync(creditId, companyA, branchA);

        Assert.True(await CreditRepository.CancelCreditAsync(creditId, companyA, branchA));
        var afterCorrectCancel = await CreditRepository.GetCreditByIdAsync(creditId, companyA, branchA);

        Assert.NotNull(afterWrongCancel);
        Assert.Equal("pending", afterWrongCancel!.Status);
        Assert.NotNull(afterCorrectCancel);
        Assert.Equal("cancelled", afterCorrectCancel!.Status);
    }

    private async Task<long> SeedCreditAsync(
        long companyId,
        long branchId,
        string customerName,
        string orderNumber,
        string status,
        decimal originalTotal,
        decimal amountPaid,
        long? orderId = null)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.credits (
                company_id, branch_id, order_id, customer_name, order_number,
                original_total, amount_paid, credit_amount, status, notes, created_by, created_at
            )
            VALUES (
                @companyId, @branchId, @orderId, @customerName, @orderNumber,
                @originalTotal, @amountPaid, @creditAmount, @status, NULL, 1, NOW()
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@orderId", orderId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@customerName", customerName);
        cmd.Parameters.AddWithValue("@orderNumber", orderNumber);
        cmd.Parameters.AddWithValue("@originalTotal", originalTotal);
        cmd.Parameters.AddWithValue("@amountPaid", amountPaid);
        cmd.Parameters.AddWithValue("@creditAmount", originalTotal - amountPaid);
        cmd.Parameters.AddWithValue("@status", status);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed credit"));
    }

    private async Task<long> SeedOrderAsync(long companyId, long branchId, string orderNumber)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var tableCommand = new NpgsqlCommand(@"
            INSERT INTO sales.tables (company_id, branch_id, table_number, name, status, created_by)
            VALUES (@companyId, @branchId, @tableNumber, @name, 'closed', 1)
            RETURNING id", (NpgsqlConnection)conn);
        tableCommand.Parameters.AddWithValue("@companyId", companyId);
        tableCommand.Parameters.AddWithValue("@branchId", branchId);
        tableCommand.Parameters.AddWithValue("@tableNumber", Random.Shared.Next(1000, 9999));
        tableCommand.Parameters.AddWithValue("@name", $"Mesa {orderNumber}");
        var tableId = (long)(await tableCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed table"));

        using var orderCommand = new NpgsqlCommand(@"
            INSERT INTO sales.orders
                (company_id, branch_id, table_id, order_number, status, subtotal, total, final_total_paid, created_by)
            VALUES
                (@companyId, @branchId, @tableId, @orderNumber, 'invoiced', 25000, 25000, 10000, 1)
            RETURNING id", (NpgsqlConnection)conn);
        orderCommand.Parameters.AddWithValue("@companyId", companyId);
        orderCommand.Parameters.AddWithValue("@branchId", branchId);
        orderCommand.Parameters.AddWithValue("@tableId", tableId);
        orderCommand.Parameters.AddWithValue("@orderNumber", orderNumber);
        return (long)(await orderCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed order"));
    }
}
