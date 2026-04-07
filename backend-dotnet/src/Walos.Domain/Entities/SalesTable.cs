namespace Walos.Domain.Entities;

public class SalesTable : BaseEntity
{
    public long BranchId { get; set; }
    public int TableNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "open"; // open, invoiced, cancelled
    public long? CreatedBy { get; set; }

    // Navigation (from JOINs)
    public List<OrderItem>? Items { get; set; }
    public decimal? Total { get; set; }
}
