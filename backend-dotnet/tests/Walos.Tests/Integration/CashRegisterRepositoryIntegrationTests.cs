using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class CashRegisterRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetActiveByUserAsync_Should_Return_Only_Open_Register_For_Requested_Company_Branch_And_User()
    {
        var companyA = await SeedCompanyAsync("Cash Co A");
        var companyB = await SeedCompanyAsync("Cash Co B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB1 = await SeedBranchAsync(companyB, "B-1");
        var userA1 = await SeedUserAsync(companyA, branchA1, $"cash-a1-{Guid.NewGuid():N}@test.com");
        var userA2 = await SeedUserAsync(companyA, branchA2, $"cash-a2-{Guid.NewGuid():N}@test.com");
        var userB1 = await SeedUserAsync(companyB, branchB1, $"cash-b1-{Guid.NewGuid():N}@test.com");

        var registerA1 = await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = companyA,
            BranchId = branchA1,
            OpenedBy = userA1,
            Status = "open",
            OpeningAmount = 100000m,
            OpenedAt = DateTime.UtcNow
        });

        await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = companyA,
            BranchId = branchA2,
            OpenedBy = userA2,
            Status = "open",
            OpeningAmount = 200000m,
            OpenedAt = DateTime.UtcNow
        });

        await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = companyB,
            BranchId = branchB1,
            OpenedBy = userB1,
            Status = "open",
            OpeningAmount = 300000m,
            OpenedAt = DateTime.UtcNow
        });

        var active = await CashRegisterRepository.GetActiveByUserAsync(companyA, branchA1, userA1);
        var wrongBranch = await CashRegisterRepository.GetActiveByUserAsync(companyA, branchA2, userA1);
        var wrongCompany = await CashRegisterRepository.GetActiveByUserAsync(companyB, branchB1, userA1);

        Assert.NotNull(active);
        Assert.Equal(registerA1.Id, active!.Id);
        Assert.Equal(companyA, active.CompanyId);
        Assert.Equal(branchA1, active.BranchId);
        Assert.Null(wrongBranch);
        Assert.Null(wrongCompany);
    }

    [SkippableFact]
    public async Task GetHistoryAsync_Should_Return_Only_Registers_For_Requested_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("History Co A");
        var companyB = await SeedCompanyAsync("History Co B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB1 = await SeedBranchAsync(companyB, "B-1");
        var userA1 = await SeedUserAsync(companyA, branchA1, $"history-a1-{Guid.NewGuid():N}@test.com");
        var userA2 = await SeedUserAsync(companyA, branchA2, $"history-a2-{Guid.NewGuid():N}@test.com");
        var userB1 = await SeedUserAsync(companyB, branchB1, $"history-b1-{Guid.NewGuid():N}@test.com");

        await SeedCashRegisterAsync(companyA, branchA1, userA1, "closed", 50000m);
        await SeedCashRegisterAsync(companyA, branchA2, userA2, "closed", 60000m);
        await SeedCashRegisterAsync(companyB, branchB1, userB1, "closed", 70000m);

        var history = (await CashRegisterRepository.GetHistoryAsync(companyA, branchA1, null, null, 1, 20)).ToList();
        var count = await CashRegisterRepository.GetHistoryCountAsync(companyA, branchA1, null, null);

        Assert.Single(history);
        Assert.Equal(1, count);
        Assert.Equal(companyA, history[0].CompanyId);
        Assert.Equal(branchA1, history[0].BranchId);
    }

    [SkippableFact]
    public async Task GetMovementsAsync_Should_Not_Return_Movements_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Movements Co A");
        var companyB = await SeedCompanyAsync("Movements Co B");
        var branchA = await SeedBranchAsync(companyA, "A");
        var branchB = await SeedBranchAsync(companyB, "B");
        var userA = await SeedUserAsync(companyA, branchA, $"moves-a-{Guid.NewGuid():N}@test.com");
        var userB = await SeedUserAsync(companyB, branchB, $"moves-b-{Guid.NewGuid():N}@test.com");

        var registerA = await SeedCashRegisterAsync(companyA, branchA, userA, "open", 50000m);
        var registerB = await SeedCashRegisterAsync(companyB, branchB, userB, "open", 80000m);

        await SeedCashMovementAsync(companyA, registerA, "in", 10000m, "Ingreso A", userA);
        await SeedCashMovementAsync(companyB, registerB, "out", 9000m, "Salida B", userB);

        var movements = (await CashRegisterRepository.GetMovementsAsync(registerA, companyA)).ToList();

        Assert.Single(movements);
        Assert.Equal(companyA, movements[0].CompanyId);
        Assert.Equal(registerA, movements[0].CashRegisterId);
        Assert.Equal("Ingreso A", movements[0].Reason);
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
                @companyId, @branchId, @openedBy, CASE WHEN @status = 'closed' THEN @openedBy ELSE NULL END, @status, @openingAmount, 
                CASE WHEN @status = 'closed' THEN @openingAmount ELSE NULL END,
                CASE WHEN @status = 'closed' THEN @openingAmount ELSE NULL END,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, NULL, NOW(),
                CASE WHEN @status = 'closed' THEN NOW() ELSE NULL END, NOW(), NOW()
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

    private async Task<long> SeedCashMovementAsync(long companyId, long cashRegisterId, string type, decimal amount, string reason, long createdBy)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.cash_movements (
                company_id, cash_register_id, type, amount, reason, notes, created_by, created_at
            )
            VALUES (
                @companyId, @cashRegisterId, @type, @amount, @reason, NULL, @createdBy, NOW()
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@cashRegisterId", cashRegisterId);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@createdBy", createdBy);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed cash movement"));
    }
}
