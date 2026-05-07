using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class SalesRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetActiveTablesAsync_Should_Return_Only_Open_Tables_For_Requested_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Sales Co A");
        var companyB = await SeedCompanyAsync("Sales Co B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB1 = await SeedBranchAsync(companyB, "B-1");

        var openInRequestedBranch = await SeedTableAsync(companyA, branchA1, 1, "Mesa A1", "open");
        await SeedTableAsync(companyA, branchA2, 2, "Mesa A2", "open");
        await SeedTableAsync(companyB, branchB1, 3, "Mesa B1", "open");
        await SeedTableAsync(companyA, branchA1, 4, "Mesa cerrada", "closed");

        var tables = (await SalesRepository.GetActiveTablesAsync(companyA, branchA1)).ToList();

        Assert.Single(tables);
        Assert.Equal(openInRequestedBranch, tables[0].Id);
        Assert.Equal(companyA, tables[0].CompanyId);
        Assert.Equal(branchA1, tables[0].BranchId);
        Assert.Equal("open", tables[0].Status);
    }

    [SkippableFact]
    public async Task GetTableByIdAsync_Should_Return_Null_For_Table_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Table Co A");
        var companyB = await SeedCompanyAsync("Table Co B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");

        var tableId = await SeedTableAsync(companyA, branchA, 10, "Mesa privada", "open");

        var wrongCompanyRead = await SalesRepository.GetTableByIdAsync(tableId, companyB);
        var correctCompanyRead = await SalesRepository.GetTableByIdAsync(tableId, companyA);

        Assert.Null(wrongCompanyRead);
        Assert.NotNull(correctCompanyRead);
        Assert.Equal(companyA, correctCompanyRead!.CompanyId);
    }

    [SkippableFact]
    public async Task GetOrderByTableIdAsync_Should_Return_Only_Pending_Order_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Order Co A");
        var companyB = await SeedCompanyAsync("Order Co B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");
        var branchB = await SeedBranchAsync(companyB, "Branch B");

        var tableA = await SeedTableAsync(companyA, branchA, 20, "Mesa A", "open");
        var tableB = await SeedTableAsync(companyB, branchB, 30, "Mesa B", "open");

        await SeedOrderAsync(companyA, branchA, tableA, "A-001", "pending", 25000m);
        await SeedOrderAsync(companyB, branchB, tableB, "B-001", "pending", 45000m);

        var orderA = await SalesRepository.GetOrderByTableIdAsync(tableA, companyA);
        var orderWrongCompany = await SalesRepository.GetOrderByTableIdAsync(tableA, companyB);

        Assert.NotNull(orderA);
        Assert.Equal(companyA, orderA!.CompanyId);
        Assert.Equal("A-001", orderA.OrderNumber);
        Assert.Null(orderWrongCompany);
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

    private async Task<long> SeedOrderAsync(long companyId, long branchId, long tableId, string orderNumber, string status, decimal total)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales.orders (company_id, branch_id, table_id, order_number, status, subtotal, tax, total, created_by, created_at)
            VALUES (@companyId, @branchId, @tableId, @orderNumber, @status, @total, 0, @total, 1, NOW())
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@tableId", tableId);
        cmd.Parameters.AddWithValue("@orderNumber", orderNumber);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@total", total);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed order"));
    }
}
