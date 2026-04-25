namespace Walos.Domain.Entities;

public class Refund : BaseEntity
{
    public long BranchId { get; set; }
    public long OrderId { get; set; }
    public string RefundType { get; set; } = "full"; // full | partial
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "completed"; // completed | pending_approval
    public long? ApprovedBy { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public List<RefundItem>? Items { get; set; }
}

public class RefundItem
{
    public long Id { get; set; }
    public long RefundId { get; set; }
    public long OrderItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public DateTime CreatedAt { get; set; }
}
