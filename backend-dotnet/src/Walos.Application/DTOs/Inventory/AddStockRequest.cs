namespace Walos.Application.DTOs.Inventory;

public class AddStockRequest
{
    public long ProductId { get; set; }
    public long? BranchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
}
