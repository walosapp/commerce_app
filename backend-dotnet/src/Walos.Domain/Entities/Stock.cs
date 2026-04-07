namespace Walos.Domain.Entities;

public class Stock : BaseEntity
{
    public long BranchId { get; set; }
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public string? Location { get; set; }

    // Navigation properties (read-only, from JOINs)
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public string? Category { get; set; }
    public decimal? MinStock { get; set; }
    public string? Unit { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? StockStatus { get; set; }
}
