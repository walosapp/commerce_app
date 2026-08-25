using Npgsql;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

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
    public async Task GetStockByBranchAsync_Expands_Open_Prepared_Order_Into_Ingredient_Commitment()
    {
        var company = await SeedCompanyAsync("Prepared commitment company");
        var branch = await SeedBranchAsync(company, "Prepared commitment branch");
        var user = await SeedUserAsync(company, branch, $"prepared-{Guid.NewGuid():N}@test.com");
        var category = await SeedInventoryCategoryAsync(company, "Prepared commitment category");
        var unit = await SeedInventoryUnitAsync(company, "Prepared unit", "pru");
        var ingredient = await SeedProductAsync(company, category, unit, "Ingredient", "ING-COMMIT", 0, 0);
        var prepared = await SeedProductAsync(company, category, unit, "Prepared", "PREP-COMMIT", 0, 0);
        await SeedStockAsync(company, branch, ingredient, 10m);

        using (var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync())
        using (var cmd = new NpgsqlCommand(@"
            UPDATE inventory.products
            SET product_type='prepared', track_stock=FALSE
            WHERE id=@prepared AND company_id=@company;
            INSERT INTO inventory.recipes (company_id,product_id,ingredient_id,quantity,unit_id)
            VALUES (@company,@prepared,@ingredient,2,@unit);
            WITH inserted_table AS (
                INSERT INTO sales.tables (company_id,branch_id,table_number,name,status,created_by)
                VALUES (@company,@branch,99,'Prepared commitment','open',@user)
                RETURNING id
            ), inserted_order AS (
                INSERT INTO sales.orders (company_id,branch_id,table_id,order_number,status,subtotal,total,created_by)
                SELECT @company,@branch,id,'PREP-COMMIT','pending',100,100,@user FROM inserted_table
                RETURNING id
            )
            INSERT INTO sales.order_items (company_id,order_id,product_id,product_name,quantity,unit_price)
            SELECT @company,id,@prepared,'Prepared',1,100 FROM inserted_order;", conn))
        {
            cmd.Parameters.AddWithValue("@company", company);
            cmd.Parameters.AddWithValue("@branch", branch);
            cmd.Parameters.AddWithValue("@user", user);
            cmd.Parameters.AddWithValue("@unit", unit);
            cmd.Parameters.AddWithValue("@ingredient", ingredient);
            cmd.Parameters.AddWithValue("@prepared", prepared);
            await cmd.ExecuteNonQueryAsync();
        }

        var stock = Assert.Single(
            await InventoryRepository.GetStockByBranchAsync(branch, company),
            item => item.ProductId == ingredient);
        Assert.Equal(10m, stock.Quantity);
        Assert.Equal(2m, stock.ReservedQuantity);
        Assert.Equal(8m, stock.AvailableQuantity);
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
        await SeedStockAsync(companyId, branch1, okProduct, 20);
        await SeedStockAsync(companyId, branch2, lowProduct, 20);
        await SeedStockAsync(companyId, branch2, okProduct, 20);

        var branch1Alerts = (await InventoryRepository.GetActiveAlertsAsync(companyId, branch1)).ToList();
        var branch2Alerts = (await InventoryRepository.GetActiveAlertsAsync(companyId, branch2)).ToList();

        Assert.Contains(branch1Alerts, a => a.ProductName == "Low Product");
        Assert.DoesNotContain(branch1Alerts, a => a.ProductName == "Ok Product");
        Assert.Empty(branch2Alerts);
    }

    [SkippableFact]
    public async Task CreateProductAsync_Should_Reject_Category_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Product Scope A");
        var companyB = await SeedCompanyAsync("Product Scope B");
        var categoryB = await SeedInventoryCategoryAsync(companyB, "Foreign Cat");
        var unitA = await SeedInventoryUnitAsync(companyA, "Local Unit", "lu");

        await Assert.ThrowsAsync<NotFoundException>(() => InventoryRepository.CreateProductAsync(new Product
        {
            CompanyId = companyA,
            Name = "Invalid product",
            Sku = $"INV-{Guid.NewGuid():N}"[..30],
            CategoryId = categoryB,
            UnitId = unitA,
            ProductType = "simple",
            IsForSale = true,
            TrackStock = true,
        }));

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM inventory.products WHERE company_id = @companyId AND name = 'Invalid product'",
            (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyA);
        Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync() ?? -1L));
    }

    [SkippableFact]
    public async Task UpdateProductAsync_Should_Reject_Foreign_Unit_Without_Changing_Product()
    {
        var companyA = await SeedCompanyAsync("Product Update A");
        var companyB = await SeedCompanyAsync("Product Update B");
        var categoryA = await SeedInventoryCategoryAsync(companyA, "Local Cat");
        var unitA = await SeedInventoryUnitAsync(companyA, "Local Unit", "ua2");
        var unitB = await SeedInventoryUnitAsync(companyB, "Foreign Unit", "ub2");
        var productId = await SeedProductAsync(companyA, categoryA, unitA, "Original", "ORIGINAL", 1, 2);
        var product = await InventoryRepository.GetProductByIdAsync(productId, companyA);
        Assert.NotNull(product);
        product!.Name = "Attempted update";
        product.UnitId = unitB;

        await Assert.ThrowsAsync<NotFoundException>(() => InventoryRepository.UpdateProductAsync(product));

        var persisted = await InventoryRepository.GetProductByIdAsync(productId, companyA);
        Assert.NotNull(persisted);
        Assert.Equal("Original", persisted!.Name);
        Assert.Equal(unitA, persisted.UnitId);
    }

    [SkippableFact]
    public async Task TryUpdateProductImageAsync_Should_RequireTenantAndExpectedReference()
    {
        var companyA = await SeedCompanyAsync("Product Image A");
        var companyB = await SeedCompanyAsync("Product Image B");
        var categoryA = await SeedInventoryCategoryAsync(companyA, "Image Cat A");
        var unitA = await SeedInventoryUnitAsync(companyA, "Image Unit A", "iua");
        var productA = await SeedProductAsync(
            companyA, categoryA, unitA, "Image Product A", $"IMG-{Guid.NewGuid():N}", 0, 0);
        var firstKey = $"companies/{companyA}/products/{productA}/first.webp";
        var staleKey = $"companies/{companyA}/products/{productA}/stale.webp";

        var firstUpdate = await InventoryRepository.TryUpdateProductImageAsync(
            productA, companyA, null, firstKey);
        var staleUpdate = await InventoryRepository.TryUpdateProductImageAsync(
            productA, companyA, null, staleKey);
        var foreignUpdate = await InventoryRepository.TryUpdateProductImageAsync(
            productA, companyB, firstKey, staleKey);

        Assert.True(firstUpdate);
        Assert.False(staleUpdate);
        Assert.False(foreignUpdate);
        var persisted = await InventoryRepository.GetProductByIdAsync(productA, companyA);
        Assert.Equal(firstKey, persisted?.ImageUrl);
    }

    private async Task<long> SeedInventoryCategoryAsync(long companyId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.categories (company_id, name, code, is_active, created_by)
            VALUES (@companyId, @name, @code, true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@code", $"TEST-{Guid.NewGuid():N}");
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed inventory category"));
    }

    private async Task<long> SeedInventoryUnitAsync(long companyId, string name, string abbreviation)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active, created_by)
            VALUES (@companyId, @name, @abbreviation, 'quantity', true, 1)
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
