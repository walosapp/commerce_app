using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class CreditPaymentAtomicIntegrationTests : IntegrationTestBase
{
    private CreditService Service => new(CreditRepository, NullLogger<CreditService>.Instance);

    [SkippableFact]
    public async Task Partial_Payment_Updates_Credit_And_Persists_Method()
    {
        var ctx = await SeedCreditContextAsync("Partial", 100m, 0m);
        var result = await Service.AddPaymentAsync(ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = 40m, PaymentMethod = "card", Notes = "Tarjeta" });

        Assert.Equal(60m, result.CreditAmount);
        Assert.Equal(40m, result.AmountPaid);
        Assert.Equal("partial", result.Status);
        Assert.Single(result.Payments!);
        Assert.Equal("card", result.Payments![0].PaymentMethod);
        Assert.Null(result.Payments[0].CashRegisterId);
    }

    [SkippableFact]
    public async Task Total_Payment_Marks_Credit_Paid()
    {
        var ctx = await SeedCreditContextAsync("Total", 100m, 40m);
        var result = await Service.AddPaymentAsync(ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = 60m, PaymentMethod = "transfer" });

        Assert.Equal(0m, result.CreditAmount);
        Assert.Equal(100m, result.AmountPaid);
        Assert.Equal("paid", result.Status);
        Assert.NotNull(result.PaidAt);
    }

    [SkippableFact]
    public async Task Payment_Above_Balance_Is_Rejected_Without_Insert()
    {
        var ctx = await SeedCreditContextAsync("Over", 100m, 40m);
        await Assert.ThrowsAsync<ValidationException>(() => Service.AddPaymentAsync(
            ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = 61m, PaymentMethod = "card" }));
        Assert.Equal(0, await CountPaymentsAsync(ctx.CreditId));
    }

    [SkippableFact]
    public async Task Zero_Payment_Is_Rejected()
    {
        var ctx = await SeedCreditContextAsync("Zero", 100m, 0m);
        await Assert.ThrowsAsync<ValidationException>(() => Service.AddPaymentAsync(
            ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = 0m, PaymentMethod = "cash" }));
        Assert.Equal(0, await CountPaymentsAsync(ctx.CreditId));
    }

    [SkippableFact]
    public async Task Negative_Payment_Is_Rejected()
    {
        var ctx = await SeedCreditContextAsync("Negative", 100m, 0m);
        await Assert.ThrowsAsync<ValidationException>(() => Service.AddPaymentAsync(
            ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = -1m, PaymentMethod = "cash" }));
        Assert.Equal(0, await CountPaymentsAsync(ctx.CreditId));
    }

    [SkippableFact]
    public async Task Credit_From_Another_Tenant_Is_Not_Found()
    {
        var owner = await SeedCreditContextAsync("Owner", 100m, 0m);
        var companyB = await SeedCompanyAsync("Payment Attacker");
        var branchB = await SeedBranchAsync(companyB, "Attacker");
        var userB = await SeedUserAsync(companyB, branchB, $"attacker-{Guid.NewGuid():N}@test.com");

        await Assert.ThrowsAsync<NotFoundException>(() => Service.AddPaymentAsync(
            owner.CreditId, companyB, branchB, userB,
            new AddCreditPaymentRequest { Amount = 10m, PaymentMethod = "card" }));
        Assert.Equal(0, await CountPaymentsAsync(owner.CreditId));
    }

    [SkippableFact]
    public async Task Already_Paid_Credit_Is_Rejected()
    {
        var ctx = await SeedCreditContextAsync("Paid", 100m, 100m, "paid");
        await Assert.ThrowsAsync<BusinessException>(() => Service.AddPaymentAsync(
            ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = 1m, PaymentMethod = "card" }));
    }

    [SkippableFact]
    public async Task Concurrent_Payments_Cannot_Overpay()
    {
        var ctx = await SeedCreditContextAsync("Concurrent", 100m, 0m);

        async Task<bool> TryPayAsync()
        {
            try
            {
                await Service.AddPaymentAsync(ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
                    new AddCreditPaymentRequest { Amount = 60m, PaymentMethod = "card" });
                return true;
            }
            catch (ValidationException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryPayAsync(), TryPayAsync());
        var credit = await CreditRepository.GetCreditByIdAsync(ctx.CreditId, ctx.Company, ctx.Branch);

        Assert.Single(results.Where(x => x));
        Assert.Equal(60m, credit!.AmountPaid);
        Assert.Equal(40m, credit.CreditAmount);
        Assert.Equal(1, await CountPaymentsAsync(ctx.CreditId));
    }

    [SkippableFact]
    public async Task Credit_Update_Failure_Rolls_Back_Payment_Insert()
    {
        var ctx = await SeedCreditContextAsync("Rollback", 100m, 0m);
        var suffix = Guid.NewGuid().ToString("N");
        var functionName = $"fail_credit_update_{suffix}";
        var triggerName = $"trg_fail_credit_update_{suffix}";
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await new NpgsqlCommand($@"
            CREATE FUNCTION sales.{functionName}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.id = {ctx.CreditId} THEN RAISE EXCEPTION 'forced credit failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {triggerName} BEFORE UPDATE ON sales.credits
            FOR EACH ROW EXECUTE FUNCTION sales.{functionName}();", conn).ExecuteNonQueryAsync();

        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => Service.AddPaymentAsync(
                ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
                new AddCreditPaymentRequest { Amount = 20m, PaymentMethod = "card" }));
            Assert.Equal(0, await CountPaymentsAsync(ctx.CreditId));
        }
        finally
        {
            await new NpgsqlCommand($@"
                DROP TRIGGER IF EXISTS {triggerName} ON sales.credits;
                DROP FUNCTION IF EXISTS sales.{functionName}();", conn).ExecuteNonQueryAsync();
        }
    }

    [SkippableFact]
    public async Task Cash_Payment_Updates_CashIn_In_Same_Transaction()
    {
        var ctx = await SeedCreditContextAsync("Cash", 100m, 0m);
        var register = await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = ctx.Company,
            BranchId = ctx.Branch,
            OpenedBy = ctx.User,
            Status = "open",
            OpeningAmount = 50m,
            OpenedAt = DateTime.UtcNow
        });

        var result = await Service.AddPaymentAsync(ctx.CreditId, ctx.Company, ctx.Branch, ctx.User,
            new AddCreditPaymentRequest { Amount = 25m, PaymentMethod = " CASH " });
        var persistedRegister = await CashRegisterRepository.GetByIdAsync(register.Id, ctx.Company);

        Assert.Equal(25m, persistedRegister!.CashIn);
        Assert.Equal(0m, persistedRegister.TotalCashSales);
        Assert.Equal(register.Id, result.Payments!.Single().CashRegisterId);
        Assert.Single(await CashRegisterRepository.GetMovementsAsync(register.Id, ctx.Company, ctx.Branch));
    }

    [SkippableFact]
    public async Task Historical_Payment_Can_Keep_Null_Method_And_Register()
    {
        var ctx = await SeedCreditContextAsync("Historical", 100m, 10m);
        using (var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync())
        using (var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.credit_payments (
                company_id, credit_id, amount, notes, created_by, created_at
            ) VALUES (@company, @credit, 10, 'Historico', @user, NOW())", conn))
        {
            cmd.Parameters.AddWithValue("@company", ctx.Company);
            cmd.Parameters.AddWithValue("@credit", ctx.CreditId);
            cmd.Parameters.AddWithValue("@user", ctx.User);
            await cmd.ExecuteNonQueryAsync();
        }

        var credit = await CreditRepository.GetCreditByIdAsync(ctx.CreditId, ctx.Company, ctx.Branch);

        var historicalPayment = Assert.Single(credit!.Payments!);
        Assert.Null(historicalPayment.PaymentMethod);
        Assert.Null(historicalPayment.CashRegisterId);
    }

    private async Task<(long Company, long Branch, long User, long CreditId)> SeedCreditContextAsync(
        string prefix, decimal originalTotal, decimal amountPaid, string? status = null)
    {
        var company = await SeedCompanyAsync($"{prefix} Credit Company");
        var branch = await SeedBranchAsync(company, $"{prefix} Branch");
        var user = await SeedUserAsync(company, branch, $"{prefix.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com");
        var balance = originalTotal - amountPaid;
        status ??= balance == 0 ? "paid" : amountPaid > 0 ? "partial" : "pending";
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.credits (
                company_id, branch_id, customer_name, order_number,
                original_total, amount_paid, credit_amount, status,
                paid_at, created_by, created_at
            ) VALUES (
                @companyId, @branchId, 'Cliente', @orderNumber,
                @originalTotal, @amountPaid, @balance, @status,
                CASE WHEN @status = 'paid' THEN NOW() ELSE NULL END, @userId, NOW()
            ) RETURNING id", conn);
        cmd.Parameters.AddWithValue("@companyId", company);
        cmd.Parameters.AddWithValue("@branchId", branch);
        cmd.Parameters.AddWithValue("@orderNumber", $"CR-{Guid.NewGuid():N}"[..30]);
        cmd.Parameters.AddWithValue("@originalTotal", originalTotal);
        cmd.Parameters.AddWithValue("@amountPaid", amountPaid);
        cmd.Parameters.AddWithValue("@balance", balance);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@userId", user);
        var creditId = (long)(await cmd.ExecuteScalarAsync() ?? throw new InvalidOperationException());
        return (company, branch, user, creditId);
    }

    private async Task<int> CountPaymentsAsync(long creditId)
    {
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM sales.credit_payments WHERE credit_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", creditId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
