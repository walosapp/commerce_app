using Dapper;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class RecipeRepository : IRecipeRepository
{
    private readonly IDbConnectionFactory _db;

    public RecipeRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Recipe>> GetByProductAsync(long productId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                r.id AS Id, r.company_id AS CompanyId, r.product_id AS ProductId,
                r.ingredient_id AS IngredientId, r.quantity AS Quantity,
                r.unit_id AS UnitId, r.notes AS Notes,
                r.created_at AS CreatedAt, r.updated_at AS UpdatedAt,
                p.name AS IngredientName,
                u.abbreviation AS UnitAbbreviation, u.name AS UnitName
            FROM inventory.recipes r
            JOIN inventory.products p ON p.id = r.ingredient_id
            LEFT JOIN inventory.units u ON u.id = r.unit_id
            WHERE r.product_id = @ProductId AND r.company_id = @CompanyId
            ORDER BY p.name";
        return await conn.QueryAsync<Recipe>(sql, new { ProductId = productId, CompanyId = companyId });
    }

    public async Task<Recipe> UpsertIngredientAsync(Recipe recipe)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO inventory.recipes (company_id, product_id, ingredient_id, quantity, unit_id, notes)
            VALUES (@CompanyId, @ProductId, @IngredientId, @Quantity, @UnitId, @Notes)
            ON CONFLICT (product_id, ingredient_id) DO UPDATE
                SET quantity = EXCLUDED.quantity,
                    unit_id  = EXCLUDED.unit_id,
                    notes    = EXCLUDED.notes,
                    updated_at = NOW()
            RETURNING id AS Id, company_id AS CompanyId, product_id AS ProductId,
                      ingredient_id AS IngredientId, quantity AS Quantity,
                      unit_id AS UnitId, notes AS Notes,
                      created_at AS CreatedAt, updated_at AS UpdatedAt";
        return await conn.QuerySingleAsync<Recipe>(sql, recipe);
    }

    public async Task<bool> RemoveIngredientAsync(long productId, long ingredientId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            DELETE FROM inventory.recipes
            WHERE product_id = @ProductId AND ingredient_id = @IngredientId AND company_id = @CompanyId";
        return await conn.ExecuteAsync(sql, new { ProductId = productId, IngredientId = ingredientId, CompanyId = companyId }) > 0;
    }

    public async Task<bool> ClearRecipeAsync(long productId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = "DELETE FROM inventory.recipes WHERE product_id = @ProductId AND company_id = @CompanyId";
        return await conn.ExecuteAsync(sql, new { ProductId = productId, CompanyId = companyId }) > 0;
    }

    /// <summary>
    /// Returns all recipe lines scaled by sold quantity, for a list of sold products.
    /// Only returns rows for products with product_type = 'prepared'.
    /// </summary>
    public async Task<IEnumerable<Recipe>> GetAllIngredientsForSaleAsync(
        IEnumerable<(long ProductId, decimal Qty)> soldItems, long companyId)
    {
        var productIds = soldItems.Select(x => x.ProductId).Distinct().ToList();
        if (!productIds.Any()) return [];

        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                r.product_id AS ProductId,
                r.ingredient_id AS IngredientId,
                r.quantity AS Quantity,
                r.unit_id AS UnitId
            FROM inventory.recipes r
            JOIN inventory.products p ON p.id = r.product_id
            WHERE r.company_id = @CompanyId
              AND r.product_id = ANY(@ProductIds)
              AND p.product_type = 'prepared'";

        var rows = (await conn.QueryAsync<Recipe>(sql, new { CompanyId = companyId, ProductIds = productIds.ToArray() })).ToList();

        var result = new List<Recipe>();
        foreach (var row in rows)
        {
            var soldQty = soldItems.Where(x => x.ProductId == row.ProductId).Sum(x => x.Qty);
            result.Add(new Recipe
            {
                ProductId    = row.ProductId,
                IngredientId = row.IngredientId,
                Quantity     = row.Quantity * soldQty,
                UnitId       = row.UnitId,
            });
        }
        return result;
    }
}
