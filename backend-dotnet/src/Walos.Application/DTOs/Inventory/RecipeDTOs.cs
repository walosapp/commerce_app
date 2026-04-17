namespace Walos.Application.DTOs.Inventory;

public class UpsertRecipeIngredientRequest
{
    public long IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public long? UnitId { get; set; }
    public string? Notes { get; set; }
}

public class RecipeIngredientItem
{
    public long IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitAbbreviation { get; set; }
    public string? UnitName { get; set; }
    public string? Notes { get; set; }
}

public class ProductRecipeResponse
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public List<RecipeIngredientItem> Ingredients { get; set; } = [];
}
