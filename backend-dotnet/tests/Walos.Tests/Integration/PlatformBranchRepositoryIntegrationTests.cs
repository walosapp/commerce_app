using Walos.Application.DTOs.Admin;
using Walos.Domain.Exceptions;
using Walos.Infrastructure.Repositories;
using Dapper;

namespace Walos.Tests.Integration;

[Trait("Category", "PostgreSQL")]
public sealed class PlatformBranchRepositoryIntegrationTests : V1IntegrationTestBase
{
    private AdminRepository CreateRepository() =>
        new(ConnectionFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminRepository>.Instance);

    [SkippableFact]
    public async Task Create_List_Update_Are_Tenant_Scoped()
    {
        var companyA = await SeedCompanyAsync("Branch admin A");
        var companyB = await SeedCompanyAsync("Branch admin B");
        var actorA = await SeedUserAsync(companyA, null);
        var repository = CreateRepository();

        var created = await repository.CreateBranchAsync(companyA, Request("NORTE"), actorA);
        var listed = await repository.GetBranchesAsync(companyA);
        var hidden = await repository.GetBranchesAsync(companyB);
        var crossTenant = await repository.UpdateBranchAsync(
            companyB, created.Id, UpdateRequest("LEAKED", name: "Leaked"), actorA);

        Assert.Contains(listed, branch => branch.Id == created.Id && branch.CompanyId == companyA);
        Assert.DoesNotContain(hidden, branch => branch.Id == created.Id);
        Assert.Null(crossTenant);
    }

    [SkippableFact]
    public async Task Duplicate_Code_Is_Rejected_With_Stable_Code()
    {
        var companyId = await SeedCompanyAsync("Branch duplicate");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        await repository.CreateBranchAsync(companyId, Request("DUP"), actorId);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            repository.CreateBranchAsync(companyId, Request("DUP"), actorId));

        Assert.Equal("branch_code_conflict", error.Code);
    }

    [SkippableFact]
    public async Task Deactivation_Is_Rejected_When_Active_User_Is_Assigned()
    {
        var companyId = await SeedCompanyAsync("Branch unsafe");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var branch = await repository.CreateBranchAsync(companyId, Request("UNSAFE"), actorId);
        await repository.CreateBranchAsync(companyId, Request("SAFE"), actorId);
        await SeedUserAsync(companyId, branch.Id);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            repository.UpdateBranchAsync(companyId, branch.Id,
                UpdateRequest(branch.Code, isActive: false), actorId));

        Assert.Equal("branch_has_active_operations", error.Code);
        Assert.True((await repository.GetBranchesAsync(companyId)).Single(x => x.Id == branch.Id).IsActive);
    }

    [SkippableFact]
    public async Task Safe_Deactivation_And_Reactivation_Preserve_Branch()
    {
        var companyId = await SeedCompanyAsync("Branch safe");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var branch = await repository.CreateBranchAsync(companyId, Request("SAFE"), actorId);
        await repository.CreateBranchAsync(companyId, Request("OTHER"), actorId);

        var inactive = await repository.UpdateBranchAsync(companyId, branch.Id,
            UpdateRequest(branch.Code, isActive: false), actorId);
        var active = await repository.UpdateBranchAsync(companyId, branch.Id,
            UpdateRequest(branch.Code, isActive: true), actorId);

        Assert.False(inactive!.IsActive);
        Assert.True(active!.IsActive);
        Assert.Equal(branch.Id, active.Id);
    }

    [SkippableTheory]
    [InlineData("pending")]
    [InlineData("ordered")]
    public async Task Deactivation_Is_Rejected_When_Purchase_Order_Is_Active(string status)
    {
        var companyId = await SeedCompanyAsync($"Branch PO {status}");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var branch = await repository.CreateBranchAsync(companyId, Request($"PO{status[..3].ToUpperInvariant()}"), actorId);
        await repository.CreateBranchAsync(companyId, Request($"OTHER{status[..3].ToUpperInvariant()}"), actorId);
        await SeedPurchaseOrderAsync(companyId, branch.Id, actorId, status);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            repository.UpdateBranchAsync(companyId, branch.Id,
                UpdateRequest(branch.Code, isActive: false), actorId));

        Assert.Equal("branch_has_active_operations", error.Code);
        Assert.True((await repository.GetBranchesAsync(companyId)).Single(x => x.Id == branch.Id).IsActive);
    }

    [SkippableTheory]
    [InlineData("pending")]
    [InlineData("partial")]
    public async Task Deactivation_Is_Rejected_When_Credit_Is_Active(string status)
    {
        var companyId = await SeedCompanyAsync($"Branch credit {status}");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var branch = await repository.CreateBranchAsync(
            companyId, Request($"CR{status[..3].ToUpperInvariant()}"), actorId);
        await repository.CreateBranchAsync(
            companyId, Request($"OTHERCR{status[..3].ToUpperInvariant()}"), actorId);
        await SeedCreditAsync(companyId, branch.Id, actorId, status);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            repository.UpdateBranchAsync(companyId, branch.Id,
                UpdateRequest(branch.Code, isActive: false), actorId));

        Assert.Equal("branch_has_active_operations", error.Code);
        Assert.True((await repository.GetBranchesAsync(companyId)).Single(x => x.Id == branch.Id).IsActive);
    }

    [SkippableFact]
    public async Task Update_Replaces_Optional_Fields_And_Explicit_Null_Clears_Them()
    {
        var companyId = await SeedCompanyAsync("Branch replace");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var branch = await repository.CreateBranchAsync(companyId, Request("REPLACE"), actorId);

        var populated = UpdateRequest(branch.Code);
        populated.Email = "branch@example.com";
        populated.Phone = "+57 300 000 0000";
        populated.State = "Cundinamarca";
        populated.PostalCode = "110111";
        populated.MaxTables = 12;
        populated.MaxCapacity = 48;
        await repository.UpdateBranchAsync(companyId, branch.Id, populated, actorId);

        var cleared = UpdateRequest(branch.Code);
        cleared.Email = null;
        cleared.Phone = null;
        cleared.State = null;
        cleared.PostalCode = null;
        cleared.MaxTables = null;
        cleared.MaxCapacity = null;
        await repository.UpdateBranchAsync(companyId, branch.Id, cleared, actorId);

        var reloaded = (await repository.GetBranchesAsync(companyId)).Single(x => x.Id == branch.Id);
        Assert.Null(reloaded.Email);
        Assert.Null(reloaded.Phone);
        Assert.Null(reloaded.State);
        Assert.Null(reloaded.PostalCode);
        Assert.Null(reloaded.MaxTables);
        Assert.Null(reloaded.MaxCapacity);
    }

    [SkippableFact]
    public async Task Deactivation_Of_Last_Active_Branch_Is_Rejected_With_Stable_Code()
    {
        var companyId = await SeedCompanyAsync("Last active branch");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var branch = await repository.CreateBranchAsync(companyId, Request("LAST"), actorId);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            repository.UpdateBranchAsync(companyId, branch.Id,
                UpdateRequest(branch.Code, isActive: false), actorId));

        Assert.Equal("last_active_branch", error.Code);
        Assert.True((await repository.GetBranchesAsync(companyId)).Single(x => x.Id == branch.Id).IsActive);
    }

    [SkippableFact]
    public async Task Deactivation_Succeeds_When_Another_Active_Branch_Exists()
    {
        var companyId = await SeedCompanyAsync("Multiple active branches");
        var actorId = await SeedUserAsync(companyId, null);
        var repository = CreateRepository();
        var first = await repository.CreateBranchAsync(companyId, Request("FIRST"), actorId);
        await repository.CreateBranchAsync(companyId, Request("SECOND"), actorId);

        var updated = await repository.UpdateBranchAsync(companyId, first.Id,
            UpdateRequest(first.Code, isActive: false), actorId);

        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }

    [SkippableFact]
    public async Task Concurrent_Deactivation_Attempts_Leave_Exactly_One_Active_Branch()
    {
        var companyId = await SeedCompanyAsync("Concurrent active branches");
        var actorId = await SeedUserAsync(companyId, null);
        var first = await CreateRepository().CreateBranchAsync(companyId, Request("CONC-A"), actorId);
        var second = await CreateRepository().CreateBranchAsync(companyId, Request("CONC-B"), actorId);

        async Task<string> DeactivateAsync(BranchAdminResponse branch)
        {
            try
            {
                await CreateRepository().UpdateBranchAsync(companyId, branch.Id,
                    UpdateRequest(branch.Code, isActive: false), actorId);
                return "updated";
            }
            catch (BusinessException exception)
            {
                return exception.Code ?? "business_error";
            }
        }

        var results = await Task.WhenAll(DeactivateAsync(first), DeactivateAsync(second));
        var branches = await CreateRepository().GetBranchesAsync(companyId);

        Assert.Single(results, result => result == "updated");
        Assert.Single(results, result => result == "last_active_branch");
        Assert.Single(branches, branch => branch.IsActive);
    }

    private async Task SeedPurchaseOrderAsync(long companyId, long branchId, long actorId, string status)
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        var supplierId = await connection.ExecuteScalarAsync<long>(@"
            INSERT INTO suppliers.suppliers (company_id, branch_id, name, is_active, created_by)
            VALUES (@CompanyId, @BranchId, @Name, TRUE, @ActorId)
            RETURNING id", new
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = $"Supplier {Guid.NewGuid():N}",
            ActorId = actorId
        });

        await connection.ExecuteAsync(@"
            INSERT INTO suppliers.purchase_orders
                (company_id, branch_id, supplier_id, order_number, status, created_by)
            VALUES
                (@CompanyId, @BranchId, @SupplierId, @OrderNumber, @Status, @ActorId)", new
        {
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            OrderNumber = $"PO-{Guid.NewGuid():N}",
            Status = status,
            ActorId = actorId
        });
    }

    private async Task SeedCreditAsync(long companyId, long branchId, long actorId, string status)
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(@"
            INSERT INTO sales.credits
                (company_id, branch_id, customer_name, order_number, original_total,
                 amount_paid, credit_amount, status, created_by)
            VALUES
                (@CompanyId, @BranchId, @CustomerName, @OrderNumber, 100,
                 @AmountPaid, @CreditAmount, @Status, @ActorId)", new
        {
            CompanyId = companyId,
            BranchId = branchId,
            CustomerName = $"Customer {Guid.NewGuid():N}",
            OrderNumber = $"CR-{Guid.NewGuid():N}",
            AmountPaid = status == "partial" ? 40m : 0m,
            CreditAmount = status == "partial" ? 60m : 100m,
            Status = status,
            ActorId = actorId
        });
    }

    private static CreateBranchAdminRequest Request(string code) => new()
    {
        Name = $"Branch {code}",
        Code = code,
        BranchType = "general",
        Address = "Calle 1",
        City = "Bogota",
        Country = "CO",
        MaxTables = 10,
        MaxCapacity = 40
    };

    private static UpdateBranchAdminRequest UpdateRequest(
        string code,
        string name = "Updated Branch",
        bool isActive = true) => new()
    {
        Name = name,
        Code = code,
        BranchType = "general",
        Address = "Calle 2",
        City = "Bogota",
        Country = "CO",
        MaxTables = 10,
        MaxCapacity = 40,
        IsActive = isActive
    };
}
