using Dapper;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class RecipeRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Upsert_Should_Create_Recipe_When_All_References_Belong_To_Company()
    {
        var company = await SeedCompanyAsync("Recipe Valid");
        var category = await SeedCategoryAsync(company, "Recipe Valid Cat");
        var unit = await SeedUnitAsync(company, "rv");
        var product = await SeedProductAsync(company, category, unit, "Prepared", "PREP", "prepared");
        var ingredient = await SeedProductAsync(company, category, unit, "Ingredient", "ING", "supply");

        var result = await RecipeRepository.UpsertIngredientAsync(new Recipe
        {
            CompanyId = company,
            ProductId = product,
            IngredientId = ingredient,
            Quantity = 1.5m,
            UnitId = unit,
        });

        Assert.True(result.Id > 0);
        Assert.Equal(company, result.CompanyId);
        Assert.Equal(product, result.ProductId);
        Assert.Equal(ingredient, result.IngredientId);
    }

    [SkippableFact]
    public async Task Upsert_Should_Not_Update_Another_Tenants_Existing_Recipe_On_Conflict()
    {
        var companyA = await SeedCompanyAsync("Recipe Guard A");
        var companyB = await SeedCompanyAsync("Recipe Guard B");
        var categoryA = await SeedCategoryAsync(companyA, "Recipe A");
        var unitA = await SeedUnitAsync(companyA, "ra");
        var categoryB = await SeedCategoryAsync(companyB, "Recipe B");
        var unitB = await SeedUnitAsync(companyB, "rb");
        var productB = await SeedProductAsync(companyB, categoryB, unitB, "Prepared B", "PREP-B", "prepared");
        var ingredientB = await SeedProductAsync(companyB, categoryB, unitB, "Ingredient B", "ING-B", "supply");

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        var recipeB = await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO inventory.recipes (company_id, product_id, ingredient_id, quantity, unit_id, notes)
            VALUES (@CompanyId, @ProductId, @IngredientId, 3, @UnitId, 'tenant-b')
            RETURNING id", new { CompanyId = companyB, ProductId = productB, IngredientId = ingredientB, UnitId = unitB });

        await Assert.ThrowsAsync<NotFoundException>(() => RecipeRepository.UpsertIngredientAsync(new Recipe
        {
            CompanyId = companyA,
            ProductId = productB,
            IngredientId = ingredientB,
            Quantity = 99,
            UnitId = unitA,
            Notes = "attempted-overwrite",
        }));

        var persisted = await conn.QuerySingleAsync<Recipe>(@"
            SELECT quantity AS Quantity, notes AS Notes
            FROM inventory.recipes
            WHERE id = @Id", new { Id = recipeB });

        Assert.Equal(3m, persisted.Quantity);
        Assert.Equal("tenant-b", persisted.Notes);
    }

    private async Task<long> SeedCategoryAsync(long companyId, string name)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO inventory.categories (company_id, name, code, is_active)
            VALUES (@CompanyId, @Name, @Code, TRUE)
            RETURNING id", new { CompanyId = companyId, Name = name, Code = $"R-{Guid.NewGuid():N}"[..20] });
    }

    private async Task<long> SeedUnitAsync(long companyId, string abbreviation)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active)
            VALUES (@CompanyId, @Name, @Abbreviation, 'quantity', TRUE)
            RETURNING id", new { CompanyId = companyId, Name = abbreviation, Abbreviation = abbreviation });
    }

    private async Task<long> SeedProductAsync(
        long companyId, long categoryId, long unitId, string name, string sku, string productType)
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        return await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO inventory.products (
                company_id, name, sku, category_id, unit_id, product_type, is_active)
            VALUES (@CompanyId, @Name, @Sku, @CategoryId, @UnitId, @ProductType, TRUE)
            RETURNING id", new { CompanyId = companyId, Name = name, Sku = $"{sku}-{Guid.NewGuid():N}"[..30], CategoryId = categoryId, UnitId = unitId, ProductType = productType });
    }
}
