using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class FinanceRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetEntriesAsync_Should_Return_Only_Entries_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Finance Co A");
        var companyB = await SeedCompanyAsync("Finance Co B");
        var branchA = await SeedBranchAsync(companyA, "Finance A");
        var branchB = await SeedBranchAsync(companyB, "Finance B");

        var categoryA = await SeedFinanceCategoryAsync(companyA, branchA, "Compras A");
        var categoryB = await SeedFinanceCategoryAsync(companyB, branchB, "Compras B");

        await SeedFinanceEntryAsync(companyA, branchA, categoryA, "expense", "Entry A", 1000m);
        await SeedFinanceEntryAsync(companyB, branchB, categoryB, "expense", "Entry B", 2000m);

        var entriesA = (await FinanceRepository.GetEntriesAsync(companyA)).ToList();
        var entriesB = (await FinanceRepository.GetEntriesAsync(companyB)).ToList();

        Assert.All(entriesA, e => Assert.Equal(companyA, e.CompanyId));
        Assert.All(entriesB, e => Assert.Equal(companyB, e.CompanyId));
        Assert.DoesNotContain(entriesA, e => e.Description == "Entry B");
        Assert.DoesNotContain(entriesB, e => e.Description == "Entry A");
    }

    [SkippableFact]
    public async Task GetEntriesAsync_Should_Filter_By_Branch_Within_Same_Company()
    {
        var companyId = await SeedCompanyAsync("Finance Branch Co");
        var branch1 = await SeedBranchAsync(companyId, "Branch 1");
        var branch2 = await SeedBranchAsync(companyId, "Branch 2");

        var category1 = await SeedFinanceCategoryAsync(companyId, branch1, "Branch 1 Cat");
        var category2 = await SeedFinanceCategoryAsync(companyId, branch2, "Branch 2 Cat");

        await SeedFinanceEntryAsync(companyId, branch1, category1, "expense", "Entry branch 1", 1500m);
        await SeedFinanceEntryAsync(companyId, branch2, category2, "expense", "Entry branch 2", 2500m);

        var branch1Entries = (await FinanceRepository.GetEntriesAsync(companyId, branch1)).ToList();
        var branch2Entries = (await FinanceRepository.GetEntriesAsync(companyId, branch2)).ToList();

        Assert.All(branch1Entries, e => Assert.Equal(branch1, e.BranchId));
        Assert.All(branch2Entries, e => Assert.Equal(branch2, e.BranchId));
        Assert.DoesNotContain(branch1Entries, e => e.Description == "Entry branch 2");
        Assert.DoesNotContain(branch2Entries, e => e.Description == "Entry branch 1");
    }

    [SkippableFact]
    public async Task GetSummaryAsync_Should_Respect_Branch_Filter()
    {
        var companyId = await SeedCompanyAsync("Finance Summary Co");
        var branch1 = await SeedBranchAsync(companyId, "Summary 1");
        var branch2 = await SeedBranchAsync(companyId, "Summary 2");

        var incomeCat1 = await SeedFinanceCategoryAsync(companyId, branch1, "Income 1", "income");
        var incomeCat2 = await SeedFinanceCategoryAsync(companyId, branch2, "Income 2", "income");

        await SeedFinanceEntryAsync(companyId, branch1, incomeCat1, "income", "Income branch 1", 1000m);
        await SeedFinanceEntryAsync(companyId, branch2, incomeCat2, "income", "Income branch 2", 3000m);

        var summary1 = await FinanceRepository.GetSummaryAsync(companyId, branch1);
        var summary2 = await FinanceRepository.GetSummaryAsync(companyId, branch2);
        var summaryAll = await FinanceRepository.GetSummaryAsync(companyId, null);

        Assert.Equal(1000m, summary1.TotalIncome);
        Assert.Equal(3000m, summary2.TotalIncome);
        Assert.Equal(4000m, summaryAll.TotalIncome);
    }

    private async Task<long> SeedFinanceCategoryAsync(long companyId, long? branchId, string name, string type = "expense")
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO finance.categories (
                company_id, branch_id, name, type, color_hex, default_amount, day_of_month,
                nature, frequency, auto_include_in_month, is_system, is_active, created_by
            )
            VALUES (
                @companyId, @branchId, @name, @type, '#111827', 0, 1,
                'fixed', 'monthly', true, false, true, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@type", type);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed finance category"));
    }

    private async Task<long> SeedFinanceEntryAsync(long companyId, long? branchId, long categoryId, string type, string description, decimal amount)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO finance.entries (
                company_id, branch_id, category_id, type, description, amount,
                entry_date, nature, frequency, notes, status, occurrence_in_month,
                is_manual, financial_item_id, created_by
            )
            VALUES (
                @companyId, @branchId, @categoryId, @type, @description, @amount,
                NOW(), 'fixed', 'monthly', NULL, 'posted', 1,
                true, NULL, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@categoryId", categoryId);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@description", description);
        cmd.Parameters.AddWithValue("@amount", amount);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed finance entry"));
    }
}
