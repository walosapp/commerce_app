namespace Walos.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public long CategoryId { get; set; }
    public long UnitId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MarginPercentage { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public bool IsPerishable { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }

    // Navigation properties (read-only, from JOINs)
    public string? CategoryName { get; set; }
    public string? UnitName { get; set; }
    public string? UnitAbbreviation { get; set; }
}
