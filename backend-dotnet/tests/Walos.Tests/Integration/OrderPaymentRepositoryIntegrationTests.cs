using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class OrderPaymentRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetByOrderAsync_Should_Return_Only_Payments_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Payments Co A");
        var companyB = await SeedCompanyAsync("Payments Co B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchB = await SeedBranchAsync(companyB, "B");

        var orderA = await SeedOrderWithCashRegisterAsync(companyA, branchA, "PAY-A");
        var orderB = await SeedOrderWithCashRegisterAsync(companyB, branchB, "PAY-B");

        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyA, OrderId = orderA.OrderId, Method = "cash", Amount = 15000m, Reference = null });
        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyB, OrderId = orderB.OrderId, Method = "card", Amount = 22000m, Reference = "B-REF" });

        var paymentsA = (await OrderPaymentRepository.GetByOrderAsync(orderA.OrderId, companyA)).ToList();
        var wrongCompany = (await OrderPaymentRepository.GetByOrderAsync(orderA.OrderId, companyB)).ToList();

        Assert.Single(paymentsA);
        Assert.Equal(companyA, paymentsA[0].CompanyId);
        Assert.Equal(orderA.OrderId, paymentsA[0].OrderId);
        Assert.Empty(wrongCompany);
    }

    [SkippableFact]
    public async Task GetSummaryByCashRegisterAsync_Should_Not_Mix_Another_Company_Register()
    {
        var companyA = await SeedCompanyAsync("Summary Co A");
        var companyB = await SeedCompanyAsync("Summary Co B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchA2 = await SeedBranchAsync(companyA, "A2");
        var branchB = await SeedBranchAsync(companyB, "B");

        var orderA = await SeedOrderWithCashRegisterAsync(companyA, branchA, "SUM-A");
        var orderB = await SeedOrderWithCashRegisterAsync(companyB, branchB, "SUM-B");

        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyA, OrderId = orderA.OrderId, Method = "cash", Amount = 12000m, Reference = null });
        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyA, OrderId = orderA.OrderId, Method = "card", Amount = 8000m, Reference = "A-1" });
        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyB, OrderId = orderB.OrderId, Method = "cash", Amount = 50000m, Reference = null });

        var summaryA = (await OrderPaymentRepository.GetSummaryByCashRegisterAsync(orderA.CashRegisterId, companyA, branchA)).ToList();
        var wrongBranch = (await OrderPaymentRepository.GetSummaryByCashRegisterAsync(orderA.CashRegisterId, companyA, branchA2)).ToList();

        Assert.Equal(2, summaryA.Count);
        Assert.Contains(summaryA, p => p.Method == "cash" && p.TotalAmount == 12000m && p.TransactionCount == 1);
        Assert.Contains(summaryA, p => p.Method == "card" && p.TotalAmount == 8000m && p.TransactionCount == 1);
        Assert.DoesNotContain(summaryA, p => p.TotalAmount == 50000m);
        Assert.Empty(wrongBranch);
    }

    [SkippableFact]
    public async Task GetSummaryByDateRangeAsync_Should_Respect_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Range Co A");
        var companyB = await SeedCompanyAsync("Range Co B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB1 = await SeedBranchAsync(companyB, "B-1");

        var orderA1 = await SeedOrderWithCashRegisterAsync(companyA, branchA1, "RNG-A1");
        var orderA2 = await SeedOrderWithCashRegisterAsync(companyA, branchA2, "RNG-A2");
        var orderB1 = await SeedOrderWithCashRegisterAsync(companyB, branchB1, "RNG-B1");

        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyA, OrderId = orderA1.OrderId, Method = "transfer", Amount = 18000m, Reference = "TR-A1" });
        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyA, OrderId = orderA2.OrderId, Method = "cash", Amount = 9000m, Reference = null });
        await OrderPaymentRepository.CreateAsync(new OrderPayment { CompanyId = companyB, OrderId = orderB1.OrderId, Method = "transfer", Amount = 7000m, Reference = "TR-B1" });

        var dateFrom = DateTime.UtcNow.AddDays(-1);
        var dateTo = DateTime.UtcNow.AddDays(1);

        var summary = (await OrderPaymentRepository.GetSummaryByDateRangeAsync(companyA, branchA1, dateFrom, dateTo)).ToList();

        Assert.Single(summary);
        Assert.Equal("transfer", summary[0].Method);
        Assert.Equal(18000m, summary[0].TotalAmount);
        Assert.Equal(1, summary[0].TransactionCount);
    }

    private async Task<(long OrderId, long CashRegisterId)> SeedOrderWithCashRegisterAsync(long companyId, long branchId, string orderNumber)
    {
        var userId = await SeedUserAsync(companyId, branchId, $"pay-{Guid.NewGuid():N}@test.com");
        var registerId = await SeedCashRegisterAsync(companyId, branchId, userId, "open", 50000m);
        var tableId = await SeedTableAsync(companyId, branchId, Random.Shared.Next(1000, 9999), $"Mesa {orderNumber}", "open");

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.orders (
                company_id, branch_id, table_id, cash_register_id, order_number, status, subtotal, tax, total, final_total_paid, created_by, created_at
            )
            VALUES (
                @companyId, @branchId, @tableId, @cashRegisterId, @orderNumber, 'completed', 20000, 0, 20000, 20000, 1, NOW()
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@tableId", tableId);
        cmd.Parameters.AddWithValue("@cashRegisterId", registerId);
        cmd.Parameters.AddWithValue("@orderNumber", orderNumber);
        var result = await cmd.ExecuteScalarAsync();
        return ((long)(result ?? throw new InvalidOperationException("Failed to seed order")), registerId);
    }

    private async Task<long> SeedTableAsync(long companyId, long branchId, int tableNumber, string name, string status)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.tables (company_id, branch_id, table_number, name, status, created_by, created_at)
            VALUES (@companyId, @branchId, @tableNumber, @name, @status, 1, NOW())
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@tableNumber", tableNumber);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@status", status);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed sales table"));
    }

    private async Task<long> SeedCashRegisterAsync(long companyId, long branchId, long openedBy, string status, decimal openingAmount)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.cash_registers (
                company_id, branch_id, opened_by, closed_by, status, opening_amount, closing_amount,
                expected_cash, difference, total_sales, total_cash_sales, total_card_sales,
                total_transfer_sales, total_other_sales, total_discounts, total_credits, total_tips,
                cash_in, cash_out, order_count, notes, opened_at, closed_at, created_at, updated_at
            )
            VALUES (
                @companyId, @branchId, @openedBy, NULL, @status, @openingAmount, NULL,
                NULL, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, NULL, NOW(), NULL, NOW(), NOW()
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@openedBy", openedBy);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@openingAmount", openingAmount);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed cash register"));
    }
}
