namespace Walos.Domain.Entities;

public class Recipe
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long ProductId { get; set; }
    public long IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public long? UnitId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public string? IngredientName { get; set; }
    public string? UnitAbbreviation { get; set; }
    public string? UnitName { get; set; }
}
