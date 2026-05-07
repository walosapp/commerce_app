using Npgsql;
using Walos.Application.DTOs.Inventory;

namespace Walos.Tests.Integration;

public class CatalogRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetCategoriesAsync_Should_Return_Only_Categories_For_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Catalog Co A");
        var companyB = await SeedCompanyAsync("Catalog Co B");

        await SeedCategoryAsync(companyA, "Bebidas");
        await SeedCategoryAsync(companyB, "Abarrotes");

        var categoriesA = (await CatalogRepository.GetCategoriesAsync(companyA)).ToList();

        Assert.Single(categoriesA);
        Assert.Equal(companyA, categoriesA[0].CompanyId);
        Assert.Equal("Bebidas", categoriesA[0].Name);
        Assert.DoesNotContain(categoriesA, c => c.Name == "Abarrotes");
    }

    [SkippableFact]
    public async Task UpdateCategoryAsync_Should_Not_Update_Category_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Catalog Update A");
        var companyB = await SeedCompanyAsync("Catalog Update B");
        var categoryId = await SeedCategoryAsync(companyA, "Licores");

        var wrongUpdate = await CatalogRepository.UpdateCategoryAsync(categoryId, companyB, new SaveCategoryRequest
        {
            Name = "Editada",
            Description = "Desc",
            Icon = "wine",
            Color = "#111111",
            DisplayOrder = 2
        });

        var correctRead = await CatalogRepository.GetCategoryByIdAsync(categoryId, companyA);

        Assert.Null(wrongUpdate);
        Assert.NotNull(correctRead);
        Assert.Equal("Licores", correctRead!.Name);
    }

    [SkippableFact]
    public async Task DeleteUnitAsync_Should_Not_Delete_Unit_From_Another_Company()
    {
        var companyA = await SeedCompanyAsync("Unit Delete A");
        var companyB = await SeedCompanyAsync("Unit Delete B");
        var unitId = await SeedUnitAsync(companyA, "Botella", "bot");

        var wrongDelete = await CatalogRepository.DeleteUnitAsync(unitId, companyB);
        var correctRead = await CatalogRepository.GetUnitByIdAsync(unitId, companyA);

        Assert.False(wrongDelete);
        Assert.NotNull(correctRead);
        Assert.Equal("Botella", correctRead!.Name);
    }

    private async Task<long> SeedCategoryAsync(long companyId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.categories (
                company_id, name, code, description, icon, color, display_order, is_active, created_at
            )
            VALUES (
                @companyId, @name, @code, NULL, NULL, '#4A90E2', 0, TRUE, NOW()
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@code", name.ToUpperInvariant().Replace(" ", "_"));
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed category"));
    }

    private async Task<long> SeedUnitAsync(long companyId, string name, string abbreviation)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory.units (
                company_id, name, abbreviation, unit_type, is_active, created_at
            )
            VALUES (
                @companyId, @name, @abbreviation, 'quantity', TRUE, NOW()
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@abbreviation", abbreviation);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed unit"));
    }
}
