using Npgsql;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class SuppliersRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task AddSupplierProductAsync_Should_Associate_Entities_From_Same_Company()
    {
        var tenant = await SeedSupplierProductTenantAsync("Add Valid");

        var result = await SuppliersRepository.AddSupplierProductAsync(tenant.CompanyId, tenant.BranchId, new SupplierProduct
        {
            SupplierId = tenant.SupplierId,
            ProductId = tenant.ProductId,
            SupplierSku = "SUP-VALID",
            UnitCost = 1500,
        });

        Assert.NotNull(result);
        Assert.Equal(tenant.SupplierId, result!.SupplierId);
        Assert.Equal(tenant.ProductId, result.ProductId);
        Assert.Equal(1, await CountSupplierProductAsync(tenant.SupplierId, tenant.ProductId));
    }

    [SkippableFact]
    public async Task AddSupplierProductAsync_Should_Reject_Supplier_And_Product_From_Different_Companies()
    {
        var tenantA = await SeedSupplierProductTenantAsync("Cross Supplier A");
        var tenantB = await SeedSupplierProductTenantAsync("Cross Product B");

        var result = await SuppliersRepository.AddSupplierProductAsync(tenantA.CompanyId, tenantA.BranchId, new SupplierProduct
        {
            SupplierId = tenantA.SupplierId,
            ProductId = tenantB.ProductId,
        });

        Assert.Null(result);
        Assert.Equal(0, await CountSupplierProductAsync(tenantA.SupplierId, tenantB.ProductId));
    }

    [SkippableFact]
    public async Task AddSupplierProductAsync_Should_Reject_Another_Company_Accessing_Foreign_Entities()
    {
        var tenantA = await SeedSupplierProductTenantAsync("Foreign Entities A");
        var tenantB = await SeedSupplierProductTenantAsync("Foreign Actor B");

        var result = await SuppliersRepository.AddSupplierProductAsync(tenantB.CompanyId, tenantB.BranchId, new SupplierProduct
        {
            SupplierId = tenantA.SupplierId,
            ProductId = tenantA.ProductId,
        });

        Assert.Null(result);
        Assert.Equal(0, await CountSupplierProductAsync(tenantA.SupplierId, tenantA.ProductId));
    }

    [SkippableFact]
    public async Task RemoveSupplierProductAsync_Should_Remove_Association_From_Same_Company()
    {
        var tenant = await SeedSupplierProductTenantAsync("Remove Valid");
        await SeedSupplierProductAsync(tenant.SupplierId, tenant.ProductId);

        var removed = await SuppliersRepository.RemoveSupplierProductAsync(
            tenant.CompanyId, tenant.BranchId, tenant.SupplierId, tenant.ProductId);

        Assert.True(removed);
        Assert.Equal(0, await CountSupplierProductAsync(tenant.SupplierId, tenant.ProductId));
    }

    [SkippableFact]
    public async Task RemoveSupplierProductAsync_Should_Reject_Another_Company_Deleting_Association()
    {
        var tenantA = await SeedSupplierProductTenantAsync("Delete Protected A");
        var tenantB = await SeedSupplierProductTenantAsync("Delete Actor B");
        await SeedSupplierProductAsync(tenantA.SupplierId, tenantA.ProductId);

        var removed = await SuppliersRepository.RemoveSupplierProductAsync(
            tenantB.CompanyId, tenantB.BranchId, tenantA.SupplierId, tenantA.ProductId);

        Assert.False(removed);
        Assert.Equal(1, await CountSupplierProductAsync(tenantA.SupplierId, tenantA.ProductId));
    }

    [SkippableFact]
    public async Task AddSupplierProductAsync_Should_Update_Existing_Association_Without_Duplicating_It()
    {
        var tenant = await SeedSupplierProductTenantAsync("Duplicate");

        var first = await SuppliersRepository.AddSupplierProductAsync(tenant.CompanyId, tenant.BranchId, new SupplierProduct
        {
            SupplierId = tenant.SupplierId,
            ProductId = tenant.ProductId,
            SupplierSku = "SUP-ORIGINAL",
            UnitCost = 1200,
        });
        var second = await SuppliersRepository.AddSupplierProductAsync(tenant.CompanyId, tenant.BranchId, new SupplierProduct
        {
            SupplierId = tenant.SupplierId,
            ProductId = tenant.ProductId,
            SupplierSku = "SUP-UPDATED",
            UnitCost = 1700,
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal("SUP-UPDATED", second.SupplierSku);
        Assert.Equal(1700m, second.UnitCost);
        Assert.Equal(1, await CountSupplierProductAsync(tenant.SupplierId, tenant.ProductId));
    }

    [SkippableFact]
    public async Task CrossTenant_Attempts_Should_Not_Modify_Existing_Association()
    {
        var tenantA = await SeedSupplierProductTenantAsync("Immutable A");
        var tenantB = await SeedSupplierProductTenantAsync("Immutable B");
        await SuppliersRepository.AddSupplierProductAsync(tenantA.CompanyId, tenantA.BranchId, new SupplierProduct
        {
            SupplierId = tenantA.SupplierId,
            ProductId = tenantA.ProductId,
            SupplierSku = "UNCHANGED",
            UnitCost = 1250,
        });

        var addResult = await SuppliersRepository.AddSupplierProductAsync(tenantB.CompanyId, tenantB.BranchId, new SupplierProduct
        {
            SupplierId = tenantA.SupplierId,
            ProductId = tenantA.ProductId,
            SupplierSku = "ATTACK",
            UnitCost = 1,
        });
        var removeResult = await SuppliersRepository.RemoveSupplierProductAsync(
            tenantB.CompanyId, tenantB.BranchId, tenantA.SupplierId, tenantA.ProductId);
        var persisted = await GetSupplierProductAsync(tenantA.SupplierId, tenantA.ProductId);

        Assert.Null(addResult);
        Assert.False(removeResult);
        Assert.NotNull(persisted);
        Assert.Equal("UNCHANGED", persisted!.Value.SupplierSku);
        Assert.Equal(1250m, persisted.Value.UnitCost);
        Assert.Equal(1, await CountSupplierProductAsync(tenantA.SupplierId, tenantA.ProductId));
    }

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

        var correctRead = await SuppliersRepository.GetByIdAsync(supplierId, companyA, branchA);
        var wrongRead = await SuppliersRepository.GetByIdAsync(supplierId, companyB, null);

        Assert.NotNull(correctRead);
        Assert.Equal(companyA, correctRead!.CompanyId);
        Assert.Null(wrongRead);
    }

    [SkippableFact]
    public async Task GetByIdAsync_Should_Hide_Supplier_From_Another_Branch()
    {
        var companyId = await SeedCompanyAsync("Supplier Branch Read");
        var branchA = await SeedBranchAsync(companyId, "Branch A");
        var branchB = await SeedBranchAsync(companyId, "Branch B");
        var supplierId = await SeedSupplierAsync(companyId, branchB, "Proveedor B");

        var result = await SuppliersRepository.GetByIdAsync(supplierId, companyId, branchA);

        Assert.Null(result);
    }

    [SkippableFact]
    public async Task UpdateAsync_Should_Not_Modify_Supplier_From_Another_Branch()
    {
        var companyId = await SeedCompanyAsync("Supplier Branch Update");
        var branchA = await SeedBranchAsync(companyId, "Branch A");
        var branchB = await SeedBranchAsync(companyId, "Branch B");
        var supplierId = await SeedSupplierAsync(companyId, branchB, "Proveedor Original");

        var result = await SuppliersRepository.UpdateAsync(new Supplier
        {
            Id = supplierId,
            CompanyId = companyId,
            Name = "Proveedor Atacado",
        }, branchA);
        var persisted = await SuppliersRepository.GetByIdAsync(supplierId, companyId, null);

        Assert.Null(result);
        Assert.NotNull(persisted);
        Assert.Equal("Proveedor Original", persisted!.Name);
    }

    [SkippableFact]
    public async Task SoftDeleteAsync_Should_Not_Delete_Supplier_From_Another_Branch()
    {
        var companyId = await SeedCompanyAsync("Supplier Branch Delete");
        var branchA = await SeedBranchAsync(companyId, "Branch A");
        var branchB = await SeedBranchAsync(companyId, "Branch B");
        var supplierId = await SeedSupplierAsync(companyId, branchB, "Proveedor Protegido");

        var deleted = await SuppliersRepository.SoftDeleteAsync(supplierId, companyId, branchA);
        var persisted = await SuppliersRepository.GetByIdAsync(supplierId, companyId, null);

        Assert.False(deleted);
        Assert.NotNull(persisted);
    }

    [SkippableFact]
    public async Task AddSupplierProductAsync_Should_Not_Associate_Supplier_From_Another_Branch()
    {
        var tenant = await SeedSupplierProductTenantAsync("Branch Add");
        var otherBranch = await SeedBranchAsync(tenant.CompanyId, "Other Branch");

        var result = await SuppliersRepository.AddSupplierProductAsync(
            tenant.CompanyId,
            otherBranch,
            new SupplierProduct { SupplierId = tenant.SupplierId, ProductId = tenant.ProductId });

        Assert.Null(result);
        Assert.Equal(0, await CountSupplierProductAsync(tenant.SupplierId, tenant.ProductId));
    }

    [SkippableFact]
    public async Task CrossBranch_Attempts_Should_Not_Modify_Existing_Association()
    {
        var tenant = await SeedSupplierProductTenantAsync("Branch Remove");
        var otherBranch = await SeedBranchAsync(tenant.CompanyId, "Other Branch");
        await SeedSupplierProductAsync(tenant.SupplierId, tenant.ProductId);

        var addResult = await SuppliersRepository.AddSupplierProductAsync(
            tenant.CompanyId,
            otherBranch,
            new SupplierProduct
            {
                SupplierId = tenant.SupplierId,
                ProductId = tenant.ProductId,
                SupplierSku = "ATTACK",
                UnitCost = 1,
            });
        var removed = await SuppliersRepository.RemoveSupplierProductAsync(
            tenant.CompanyId, otherBranch, tenant.SupplierId, tenant.ProductId);
        var persisted = await GetSupplierProductAsync(tenant.SupplierId, tenant.ProductId);

        Assert.Null(addResult);
        Assert.False(removed);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.Value.SupplierSku);
        Assert.Equal(1200m, persisted.Value.UnitCost);
        Assert.Equal(1, await CountSupplierProductAsync(tenant.SupplierId, tenant.ProductId));
    }

    [SkippableFact]
    public async Task CreateAsync_Should_Require_Active_Branch_From_Same_Company()
    {
        var companyA = await SeedCompanyAsync("Supplier Create A");
        var companyB = await SeedCompanyAsync("Supplier Create B");
        var validBranch = await SeedBranchAsync(companyA, "Valid Branch");
        var inactiveBranch = await SeedBranchAsync(companyA, "Inactive Branch");
        var foreignBranch = await SeedBranchAsync(companyB, "Foreign Branch");
        await SetBranchActiveAsync(inactiveBranch, false);

        var valid = await SuppliersRepository.CreateAsync(NewSupplier(companyA, validBranch, "Valid Supplier"));
        var inactive = await SuppliersRepository.CreateAsync(NewSupplier(companyA, inactiveBranch, "Inactive Supplier"));
        var foreign = await SuppliersRepository.CreateAsync(NewSupplier(companyA, foreignBranch, "Foreign Supplier"));

        Assert.NotNull(valid);
        Assert.Null(inactive);
        Assert.Null(foreign);
        Assert.Equal(0, await CountSupplierByNameAsync(companyA, "Inactive Supplier"));
        Assert.Equal(0, await CountSupplierByNameAsync(companyA, "Foreign Supplier"));
    }

    [SkippableFact]
    public async Task BranchScoped_Access_Should_Include_CompanyWide_Supplier()
    {
        var tenant = await SeedSupplierProductTenantAsync("Global Supplier");
        var globalSupplier = await SeedSupplierAsync(tenant.CompanyId, null, "Proveedor Global");

        var detail = await SuppliersRepository.GetByIdAsync(globalSupplier, tenant.CompanyId, tenant.BranchId);
        var association = await SuppliersRepository.AddSupplierProductAsync(
            tenant.CompanyId,
            tenant.BranchId,
            new SupplierProduct { SupplierId = globalSupplier, ProductId = tenant.ProductId });

        Assert.NotNull(detail);
        Assert.NotNull(association);
        Assert.Equal(1, await CountSupplierProductAsync(globalSupplier, tenant.ProductId));
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
        var inverseCrossTenantRead = (await SuppliersRepository.GetSuppliersForProductAsync(productA, companyB)).ToList();

        Assert.Contains(suppliersForProduct, s => s.Id == supplierA);
        Assert.DoesNotContain(suppliersForProduct, s => s.Id == supplierB);
        Assert.All(suppliersForProduct, s => Assert.Equal(companyA, s.CompanyId));
        Assert.Empty(inverseCrossTenantRead);
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

    private static Supplier NewSupplier(long companyId, long? branchId, string name) => new()
    {
        CompanyId = companyId,
        BranchId = branchId,
        Name = name,
        CreatedBy = 1,
    };

    private async Task SetBranchActiveAsync(long branchId, bool isActive)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            UPDATE core.branches
            SET is_active = @isActive
            WHERE id = @branchId", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@isActive", isActive);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int> CountSupplierByNameAsync(long companyId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM suppliers.suppliers
            WHERE company_id = @companyId AND name = @name", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
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
        cmd.Parameters.AddWithValue("@code", $"C{Guid.NewGuid():N}");
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed inventory category"));
    }

    private async Task<long> SeedInventoryUnitAsync(long companyId, string name, string abbreviation)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active, created_by)
            VALUES (@companyId, @name, @abbreviation, 'unit', true, 1)
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

    private async Task<(long CompanyId, long BranchId, long SupplierId, long ProductId)> SeedSupplierProductTenantAsync(string name)
    {
        var companyId = await SeedCompanyAsync($"Company {name}");
        var branchId = await SeedBranchAsync(companyId, $"Branch {name}");
        var categoryId = await SeedInventoryCategoryAsync(companyId, $"Category {name}");
        var abbreviation = $"u{Guid.NewGuid():N}"[..8];
        var unitId = await SeedInventoryUnitAsync(companyId, $"Unit {name}", abbreviation);
        var sku = $"SKU-{Guid.NewGuid():N}";
        var productId = await SeedProductAsync(companyId, categoryId, unitId, $"Product {name}", sku);
        var supplierId = await SeedSupplierAsync(companyId, branchId, $"Supplier {name}");
        return (companyId, branchId, supplierId, productId);
    }

    private async Task<int> CountSupplierProductAsync(long supplierId, long productId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM suppliers.supplier_products
            WHERE supplier_id = @supplierId AND product_id = @productId", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@supplierId", supplierId);
        cmd.Parameters.AddWithValue("@productId", productId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<(string? SupplierSku, decimal? UnitCost)?> GetSupplierProductAsync(long supplierId, long productId)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT supplier_sku, unit_cost
            FROM suppliers.supplier_products
            WHERE supplier_id = @supplierId AND product_id = @productId", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@supplierId", supplierId);
        cmd.Parameters.AddWithValue("@productId", productId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetDecimal(1));
    }
}
