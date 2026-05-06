namespace Walos.Application.DTOs.Sales;

public class CreateRefundRequest
{
    public long OrderId { get; set; }
    public string RefundType { get; set; } = "full"; // full | partial
    public string Reason { get; set; } = string.Empty;
    public List<RefundItemRequest>? Items { get; set; }
}

public class RefundItemRequest
{
    public long OrderItemId { get; set; }
    public decimal Quantity { get; set; }
}

public record RefundResponse(
    long Id,
    long CompanyId,
    long BranchId,
    long OrderId,
    string OrderNumber,
    string RefundType,
    decimal RefundAmount,
    string Reason,
    string Status,
    string? ApprovedByName,
    string CreatedByName,
    DateTime CreatedAt,
    List<RefundItemResponse>? Items
);

public record RefundItemResponse(
    long Id,
    long OrderItemId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal
);
