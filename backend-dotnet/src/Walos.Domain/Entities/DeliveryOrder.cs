namespace Walos.Domain.Entities;

public class DeliveryOrder : BaseEntity
{
    public long BranchId { get; set; }
    public string Source { get; set; } = "manual";
    public string? ExternalOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "new";

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }

    public DateTime? AcceptedAt { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public string? RejectedReason { get; set; }
    public string? ReturnedReason { get; set; }

    public long? CreatedBy { get; set; }

    public List<DeliveryOrderItem>? Items { get; set; }
    public List<DeliveryStatusHistory>? StatusHistory { get; set; }
}
