using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class CashRegisterRepositoryIntegrationTests : IntegrationTestBase
{
    private CashRegisterService Service => new(CashRegisterRepository, OrderPaymentRepository, NullLogger<CashRegisterService>.Instance);

    [SkippableFact]
    public async Task OpenAsync_Persists_Opening_Amount()
    {
        var (company, branch, user) = await SeedContextAsync("Open");
        var opened = await Service.OpenAsync(company, branch, user, new OpenCashRegisterRequest(125m, "Inicio"));

        Assert.Equal("open", opened.Status);
        Assert.Equal(125m, opened.OpeningAmount);
        Assert.Equal(0m, opened.CashIn);
        Assert.Equal(0m, opened.CashOut);
    }

    [SkippableFact]
    public async Task Cash_Sale_Updates_Only_Cash_And_Total_Sales()
    {
        var ctx = await CreateOpenRegisterAsync("Cash sale", 50m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 100m, 100m, 0, 0, 0, 0, 0, 0, 1);

        var register = await GetRegisterAsync(ctx.Register.Id, ctx.Company);
        Assert.Equal(100m, register.TotalSales);
        Assert.Equal(100m, register.TotalCashSales);
        Assert.Equal(0m, register.TotalCardSales);
        Assert.Equal(1, register.OrderCount);
    }

    [SkippableFact]
    public async Task Card_Sale_Does_Not_Increase_Physical_Cash()
    {
        var ctx = await CreateOpenRegisterAsync("Card sale", 50m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 100m, 0, 100m, 0, 0, 0, 0, 0, 1);
        var closed = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(50m, null));

        Assert.Equal(100m, closed.TotalSales);
        Assert.Equal(100m, closed.TotalCardSales);
        Assert.Equal(50m, closed.ExpectedCash);
        Assert.Equal(0m, closed.Difference);
    }

    [SkippableFact]
    public async Task Mixed_Sale_Separates_Cash_And_Card()
    {
        var ctx = await CreateOpenRegisterAsync("Mixed sale", 50m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 100m, 40m, 60m, 0, 0, 0, 0, 0, 1);
        var closed = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(90m, null));

        Assert.Equal(100m, closed.TotalSales);
        Assert.Equal(40m, closed.TotalCashSales);
        Assert.Equal(60m, closed.TotalCardSales);
        Assert.Equal(90m, closed.ExpectedCash);
    }

    [SkippableFact]
    public async Task Partial_Credit_Is_Not_Subtracted_From_Expected_Cash()
    {
        var ctx = await CreateOpenRegisterAsync("Credit sale", 50m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 100m, 40m, 0, 0, 0, 0, 60m, 0, 1);
        var closed = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(90m, null));

        Assert.Equal(100m, closed.TotalSales);
        Assert.Equal(60m, closed.TotalCredits);
        Assert.Equal(90m, closed.ExpectedCash);
        Assert.Equal(0m, closed.Difference);
    }

    [SkippableFact]
    public async Task Cash_In_Creates_Movement_And_Increments_Accumulator()
    {
        var ctx = await CreateOpenRegisterAsync("Cash in", 50m);
        var movement = await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User,
            new CashMovementRequest("in", 20m, "Cambio", null));

        var register = await GetRegisterAsync(ctx.Register.Id, ctx.Company);
        Assert.Equal("in", movement.Type);
        Assert.Equal(20m, register.CashIn);
        Assert.Equal(0m, register.CashOut);
        Assert.Single(await CashRegisterRepository.GetMovementsAsync(ctx.Register.Id, ctx.Company, ctx.Branch));
    }

    [SkippableFact]
    public async Task Cash_Out_Creates_Movement_And_Increments_Accumulator()
    {
        var ctx = await CreateOpenRegisterAsync("Cash out", 50m);
        await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User,
            new CashMovementRequest("out", 15m, "Compra menor", null));

        var register = await GetRegisterAsync(ctx.Register.Id, ctx.Company);
        Assert.Equal(0m, register.CashIn);
        Assert.Equal(15m, register.CashOut);
    }

    [SkippableFact]
    public async Task Multiple_Movements_Accumulate_Independently()
    {
        var ctx = await CreateOpenRegisterAsync("Many movements", 100m);
        await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("in", 20m, "Entrada 1", null));
        await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("out", 7m, "Salida 1", null));
        await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("IN", 5m, "Entrada 2", null));

        var register = await GetRegisterAsync(ctx.Register.Id, ctx.Company);
        Assert.Equal(25m, register.CashIn);
        Assert.Equal(7m, register.CashOut);
        Assert.Equal(3, (await CashRegisterRepository.GetMovementsAsync(ctx.Register.Id, ctx.Company, ctx.Branch)).Count());
    }

    [SkippableFact]
    public async Task Zero_Movement_Is_Rejected_Without_Writes()
    {
        var ctx = await CreateOpenRegisterAsync("Zero movement", 50m);
        await Assert.ThrowsAsync<ValidationException>(() => Service.AddMovementAsync(
            ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("in", 0m, "Inválido", null)));
        await AssertRegisterHasNoMovementsAsync(ctx.Register.Id, ctx.Company);
    }

    [SkippableFact]
    public async Task Negative_Movement_Is_Rejected_Without_Writes()
    {
        var ctx = await CreateOpenRegisterAsync("Negative movement", 50m);
        await Assert.ThrowsAsync<ValidationException>(() => Service.AddMovementAsync(
            ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("out", -1m, "Inválido", null)));
        await AssertRegisterHasNoMovementsAsync(ctx.Register.Id, ctx.Company);
    }

    [SkippableFact]
    public async Task Movement_On_Closed_Register_Is_Rejected()
    {
        var ctx = await CreateOpenRegisterAsync("Closed movement", 50m);
        await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(50m, null));
        await Assert.ThrowsAsync<BusinessException>(() => Service.AddMovementAsync(
            ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("in", 10m, "Tarde", null)));
        await AssertRegisterHasNoMovementsAsync(ctx.Register.Id, ctx.Company);
    }

    [SkippableFact]
    public async Task Movement_On_Another_Company_Register_Is_Rejected()
    {
        var owner = await CreateOpenRegisterAsync("Owner", 50m);
        var (companyB, branchB, userB) = await SeedContextAsync("Attacker");
        await Assert.ThrowsAsync<NotFoundException>(() => Service.AddMovementAsync(
            owner.Register.Id, companyB, branchB, userB, new CashMovementRequest("out", 10m, "Ataque", null)));
        await AssertRegisterHasNoMovementsAsync(owner.Register.Id, owner.Company);
    }

    [SkippableFact]
    public async Task Movements_Summary_And_ZReport_Source_Are_Not_Visible_From_Another_Branch()
    {
        var company = await SeedCompanyAsync("Cash Branch Scope");
        var branchA = await SeedBranchAsync(company, "A");
        var branchB = await SeedBranchAsync(company, "B");
        var userA = await SeedUserAsync(company, branchA, $"a-{Guid.NewGuid():N}@test.com");
        var register = await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = company,
            BranchId = branchA,
            OpenedBy = userA,
            Status = "open",
            OpeningAmount = 25m,
            OpenedAt = DateTime.UtcNow
        });
        await Service.AddMovementAsync(register.Id, company, branchA, userA,
            new CashMovementRequest("in", 5m, "Entrada", null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service.GetMovementsAsync(register.Id, company, branchB));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service.GetSummaryAsync(register.Id, company, branchB));

        Assert.Single(await Service.GetMovementsAsync(register.Id, company, branchA));
        Assert.Equal(register.Id, (await Service.GetSummaryAsync(register.Id, company, branchA)).Register.Id);
    }

    [SkippableFact]
    public async Task Exact_Close_Has_Zero_Difference()
    {
        var ctx = await CreateOpenRegisterAsync("Exact close", 100m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 80m, 30m, 50m, 0, 0, 0, 0, 0, 1);
        await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("in", 20m, "Entrada", null));
        await Service.AddMovementAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CashMovementRequest("out", 10m, "Salida", null));
        var closed = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(140m, null));

        Assert.Equal(140m, closed.ExpectedCash);
        Assert.Equal(0m, closed.Difference);
        Assert.Equal("closed", closed.Status);
        Assert.Equal(ctx.User, closed.ClosedBy);
        Assert.NotNull(closed.ClosedAt);
    }

    [SkippableFact]
    public async Task Close_With_Shortage_Has_Negative_Difference()
    {
        var ctx = await CreateOpenRegisterAsync("Short close", 50m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 100m, 100m, 0, 0, 0, 0, 0, 0, 1);
        var closed = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(140m, null));

        Assert.Equal(150m, closed.ExpectedCash);
        Assert.Equal(-10m, closed.Difference);
    }

    [SkippableFact]
    public async Task Close_With_Overage_Has_Positive_Difference()
    {
        var ctx = await CreateOpenRegisterAsync("Over close", 50m);
        await CashRegisterRepository.UpdateTotalsAsync(ctx.Register.Id, ctx.Company, 100m, 100m, 0, 0, 0, 0, 0, 0, 1);
        var closed = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(155m, null));

        Assert.Equal(150m, closed.ExpectedCash);
        Assert.Equal(5m, closed.Difference);
    }

    [SkippableFact]
    public async Task Movement_Insert_Failure_Rolls_Back_Accumulator()
    {
        var ctx = await CreateOpenRegisterAsync("Rollback movement", 50m);
        var invalidMovement = new CashMovement
        {
            CompanyId = ctx.Company,
            CashRegisterId = ctx.Register.Id,
            Type = "in",
            Amount = 20m,
            Reason = new string('x', 301),
            CreatedBy = ctx.User
        };

        var exception = await Assert.ThrowsAsync<PostgresException>(() => CashRegisterRepository.AddMovementAsync(invalidMovement, ctx.Branch));

        Assert.Equal("22001", exception.SqlState);
        await AssertRegisterHasNoMovementsAsync(ctx.Register.Id, ctx.Company);
    }

    [SkippableFact]
    public async Task Second_Close_Is_Controlled_And_Does_Not_Overwrite_First_Close()
    {
        var ctx = await CreateOpenRegisterAsync("Double close", 50m);
        var first = await Service.CloseAsync(ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(50m, "Primero"));
        await Assert.ThrowsAsync<BusinessException>(() => Service.CloseAsync(
            ctx.Register.Id, ctx.Company, ctx.Branch, ctx.User, new CloseCashRegisterRequest(999m, "Segundo")));

        var persisted = await GetRegisterAsync(ctx.Register.Id, ctx.Company);
        Assert.Equal(first.ClosingAmount, persisted.ClosingAmount);
        Assert.Equal(50m, persisted.ClosingAmount);
    }

    private async Task<(long Company, long Branch, long User)> SeedContextAsync(string prefix)
    {
        var company = await SeedCompanyAsync($"{prefix} Company");
        var branch = await SeedBranchAsync(company, $"{prefix} Branch");
        var user = await SeedUserAsync(company, branch, $"{prefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}@test.com");
        return (company, branch, user);
    }

    private async Task<(long Company, long Branch, long User, CashRegister Register)> CreateOpenRegisterAsync(string prefix, decimal openingAmount)
    {
        var (company, branch, user) = await SeedContextAsync(prefix);
        var register = await CashRegisterRepository.OpenAsync(new CashRegister
        {
            CompanyId = company,
            BranchId = branch,
            OpenedBy = user,
            Status = "open",
            OpeningAmount = openingAmount,
            OpenedAt = DateTime.UtcNow
        });
        return (company, branch, user, register);
    }

    private async Task<CashRegister> GetRegisterAsync(long id, long companyId) =>
        await CashRegisterRepository.GetByIdAsync(id, companyId)
        ?? throw new InvalidOperationException("Cash register was not found");

    private async Task AssertRegisterHasNoMovementsAsync(long id, long companyId)
    {
        var register = await GetRegisterAsync(id, companyId);
        Assert.Equal(0m, register.CashIn);
        Assert.Equal(0m, register.CashOut);
        Assert.Empty(await CashRegisterRepository.GetMovementsAsync(id, companyId, register.BranchId));
    }
}
