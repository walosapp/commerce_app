using Npgsql;

namespace Walos.Tests.Integration;

public class SuppliersRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetAllAsync_Should_Return_Only_Suppliers_For_Requested_Company_And_Branch()
    {
        var companyA = await SeedCompanyAsync("Suppliers Co A");
        var companyB = await SeedCompanyAsync("Suppliers Co B");
        var branchA1 = await SeedBranchAsync(companyA, "A-1");
        var branchA2 = await SeedBranchAsync(companyA, "A-2");
        var branchB1 = await SeedBranchAsync(companyB, "B-1");

        var supplierA1 = await SeedSupplierAsync(companyA, branchA1, "Proveedor A1");
        await SeedSupplierAsync(companyA, branchA2, "Proveedor A2");
        await SeedSupplierAsync(companyB, branchB1, "Proveedor B1");
        await SeedSupplierAsync(companyA, null, "Proveedor Global A");

        var suppliers = (await SuppliersRepository.GetAllAsync(companyA, branchA1)).ToList();

        Assert.Contains(suppliers, s => s.Id == supplierA1);
        Assert.Contains(suppliers, s => s.Name == "Proveedor Global A");
        Assert.DoesNotContain(suppliers, s => s.Name == "Proveedor A2");
        Assert.DoesNotContain(suppliers, s => s.Name == "Proveedor B1");
        Assert.All(suppliers, s => Assert.Equal(companyA, s.CompanyId));
    }

    [SkippableFact]
    public async Task GetByIdAsync_Should_Return_Null_For_Supplier_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Supplier Read A");
        var companyB = await SeedCompanyAsync("Supplier Read B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");

        var supplierId = await SeedSupplierAsync(companyA, branchA, "Proveedor Privado");

        var correctRead = await SuppliersRepository.GetByIdAsync(supplierId, companyA);
        var wrongRead = await SuppliersRepository.GetByIdAsync(supplierId, companyB);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongRead);
    }

    [SkippableFact]
    public async Task GetSuppliersForProductAsync_Should_Return_Only_Suppliers_From_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Suppliers Product A");
        var companyB = await SeedCompanyAsync("Suppliers Product B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");
        var branchB = await SeedBranchAsync(companyB, "Branch B");

        var categoryA = await SeedInventoryCategoryAsync(companyA, "Cat A");
        var unitA = await SeedInventoryUnitAsync(companyA, "Unit A", "ua");
        var productA = await SeedProductAsync(companyA, categoryA, unitA, "Producto A", "SKU-SA");

        var categoryB = await SeedInventoryCategoryAsync(companyB, "Cat B");
        var unitB = await SeedInventoryUnitAsync(companyB, "Unit B", "ub");
        await SeedProductAsync(companyB, categoryB, unitB, "Producto B", "SKU-SB");

        var supplierA = await SeedSupplierAsync(companyA, branchA, "Proveedor A");
        var supplierB = await SeedSupplierAsync(companyB, branchB, "Proveedor B");

        await SeedSupplierProductAsync(supplierA, productA);
        await SeedSupplierProductAsync(supplierB, productA);

        var suppliersForProduct = (await SuppliersRepository.GetSuppliersForProductAsync(productA, companyA)).ToList();

        Assert.Contains(suppliersForProduct, s => s.Id == supplierA);
        Assert.DoesNotContain(suppliersForProduct, s => s.Id == supplierB);
        Assert.All(suppliersForProduct, s => Assert.Equal(companyA, s.CompanyId));
    }

    private async Task<long> SeedSupplierAsync(long companyId, long? branchId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO suppliers.suppliers (
                company_id, branch_id, name, contact_name, phone, email, address, notes, is_active, created_by
            )
            VALUES (
                @companyId, @branchId, @name, 'Contacto', '3000000000', @email, 'Dir', NULL, true, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", $"{Guid.NewGuid():N}@test.com");
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed supplier"));
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

    private async Task<long> SeedProductAsync(long companyId, long categoryId, long unitId, string name, string sku)
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
                1, 100, 2, false, 'simple', true, true, true, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@sku", sku);
        cmd.Parameters.AddWithValue("@categoryId", categoryId);
        cmd.Parameters.AddWithValue("@unitId", unitId);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed product"));
    }

    private async Task<long> SeedSupplierProductAsync(long supplierId, long productId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO suppliers.supplier_products (supplier_id, product_id, supplier_sku, unit_cost, lead_time_days, notes)
            VALUES (@supplierId, @productId, NULL, 1200, 2, NULL)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@supplierId", supplierId);
        cmd.Parameters.AddWithValue("@productId", productId);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed supplier-product"));
    }
}
