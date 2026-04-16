namespace Walos.Domain.Entities;

public class SupplierProduct
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public long ProductId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal? UnitCost { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? ProductName { get; set; }
}
