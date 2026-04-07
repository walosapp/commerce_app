namespace Walos.Domain.Entities;

public class Order : BaseEntity
{
    public long BranchId { get; set; }
    public long TableId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, completed, cancelled
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotalPaid { get; set; }
    public int SplitReferenceCount { get; set; } = 1;
    public string? Notes { get; set; }
    public long? CreatedBy { get; set; }

    // Navigation
    public List<OrderItem>? Items { get; set; }
}
