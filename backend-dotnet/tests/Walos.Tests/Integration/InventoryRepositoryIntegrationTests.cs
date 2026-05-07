using Npgsql;

namespace Walos.Tests.Integration;

public class InventoryRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetStockByBranchAsync_Should_Return_Only_Products_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Inventory Co A");
        var companyB = await SeedCompanyAsync("Inventory Co B");
        var branchA = await SeedBranchAsync(companyA, "Inv A");
        var branchB = await SeedBranchAsync(companyB, "Inv B");

        var categoryA = await SeedInventoryCategoryAsync(companyA, "Cat A");
        var unitA = await SeedInventoryUnitAsync(companyA, "Unidad A", "ua");
        var productA = await SeedProductAsync(companyA, categoryA, unitA, "Prod A", "SKU-A", 5, 10);
        await SeedStockAsync(companyA, branchA, productA, 12);

        var categoryB = await SeedInventoryCategoryAsync(companyB, "Cat B");
        var unitB = await SeedInventoryUnitAsync(companyB, "Unidad B", "ub");
        var productB = await SeedProductAsync(companyB, categoryB, unitB, "Prod B", "SKU-B", 5, 10);
        await SeedStockAsync(companyB, branchB, productB, 20);

        var stockA = (await InventoryRepository.GetStockByBranchAsync(branchA, companyA)).ToList();

        Assert.All(stockA, s => Assert.Equal(companyA, s.CompanyId));
        Assert.Contains(stockA, s => s.ProductId == productA);
        Assert.DoesNotContain(stockA, s => s.ProductId == productB);
    }

    [SkippableFact]
    public async Task GetStockByProductAsync_Should_Return_Null_For_Product_In_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Stock Product A");
        var companyB = await SeedCompanyAsync("Stock Product B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");

        var categoryA = await SeedInventoryCategoryAsync(companyA, "Cat A");
        var unitA = await SeedInventoryUnitAsync(companyA, "Unit A", "ua");
        var productA = await SeedProductAsync(companyA, categoryA, unitA, "Product A", "SKU-PA", 2, 4);
        await SeedStockAsync(companyA, branchA, productA, 7);

        var correctRead = await InventoryRepository.GetStockByProductAsync(branchA, productA, companyA);
        var wrongCompanyRead = await InventoryRepository.GetStockByProductAsync(branchA, productA, companyB);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongCompanyRead);
    }

    [SkippableFact]
    public async Task GetActiveAlertsAsync_Should_Respect_Branch_Filter_Within_Same_Company()
    {
        var companyId = await SeedCompanyAsync("Alerts Co");
        var branch1 = await SeedBranchAsync(companyId, "Alerts 1");
        var branch2 = await SeedBranchAsync(companyId, "Alerts 2");

        var categoryId = await SeedInventoryCategoryAsync(companyId, "Alert Cat");
        var unitId = await SeedInventoryUnitAsync(companyId, "Alert Unit", "au");

        var lowProduct = await SeedProductAsync(companyId, categoryId, unitId, "Low Product", "SKU-L", 10, 15);
        var okProduct = await SeedProductAsync(companyId, categoryId, unitId, "Ok Product", "SKU-O", 2, 4);

        await SeedStockAsync(companyId, branch1, lowProduct, 3);
        await SeedStockAsync(companyId, branch2, okProduct, 20);

        var branch1Alerts = (await InventoryRepository.GetActiveAlertsAsync(companyId, branch1)).ToList();
        var branch2Alerts = (await InventoryRepository.GetActiveAlertsAsync(companyId, branch2)).ToList();

        Assert.Contains(branch1Alerts, a => a.ProductName == "Low Product");
        Assert.DoesNotContain(branch1Alerts, a => a.ProductName == "Ok Product");
        Assert.Empty(branch2Alerts);
    }

    private async Task<long> SeedInventoryCategoryAsync(long companyId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.categories (company_id, name, is_active, created_by)
            VALUES (@companyId, @name, true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed inventory category"));
    }

    private async Task<long> SeedInventoryUnitAsync(long companyId, string name, string abbreviation)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.units (company_id, name, abbreviation, is_active, created_by)
            VALUES (@companyId, @name, @abbreviation, true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@abbreviation", abbreviation);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed inventory unit"));
    }

    private async Task<long> SeedProductAsync(long companyId, long categoryId, long unitId, string name, string sku, decimal minStock, decimal reorderPoint)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.products (
                company_id, name, sku, category_id, unit_id, cost_price, sale_price,
                min_stock, max_stock, reorder_point, is_perishable, product_type,
                track_stock, is_for_sale, is_active, created_by
            )
            VALUES (
                @companyId, @name, @sku, @categoryId, @unitId, 1000, 2000,
                @minStock, 100, @reorderPoint, false, 'simple',
                true, true, true, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.Parameters.AddWithValue("@categoryId", categoryId);
        cmd.Parameters.AddWithValue("@unitId", unitId);
        cmd.Parameters.AddWithValue("@minStock", minStock);
        cmd.Parameters.AddWithValue("@reorderPoint", reorderPoint);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed product"));
    }

    private async Task<long> SeedStockAsync(long companyId, long branchId, long productId, decimal quantity)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
            VALUES (@companyId, @branchId, @productId, @quantity)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@productId", productId);
        cmd.Parameters.AddWithValue("@quantity", quantity);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed stock"));
    }
}
