namespace Walos.Application.DTOs.Sales;

public record OrderSearchRequest
{
    public long BranchId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? Status { get; init; } // invoiced, cancelled
    public string? RefundStatus { get; init; } // partial_refund, full_refund
    public string? PaymentMethod { get; init; }
    public string? Search { get; init; } // order number, table name
    public decimal? MinTotal { get; init; }
    public decimal? MaxTotal { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
    public string SortBy { get; init; } = "created_at";
    public string SortDir { get; init; } = "desc";
}

public record OrderDetailResponse(
    long Id,
    string OrderNumber,
    string TableName,
    int TableNumber,
    string Status,
    decimal Subtotal,
    string? DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal FinalTotalPaid,
    decimal TipAmount,
    bool TipIncluded,
    int SplitReferenceCount,
    string? RefundStatus,
    string? PaymentMethod,
    bool HasCredit,
    DateTime CreatedAt,
    string? CashierName,
    List<ReceiptItemDto> Items,
    List<ReceiptPaymentDto> Payments,
    List<RefundSummaryDto>? Refunds
);

public record RefundSummaryDto(
    long Id,
    string RefundType,
    decimal TotalAmount,
    string? Reason,
    string Status,
    DateTime CreatedAt
);

public record OrderExportRow
{
    public long Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalTotalPaid { get; init; }
    public decimal TipAmount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? RefundStatus { get; init; }
    public DateTime CreatedAt { get; init; }
}
