namespace Walos.Application.DTOs.Inventory;

public class UpdateProductRequest
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
    public int? ShelfLifeDays { get; set; }
    public string ProductType { get; set; } = "simple";
    public bool TrackStock { get; set; } = true;
    public bool IsForSale { get; set; } = true;
}
