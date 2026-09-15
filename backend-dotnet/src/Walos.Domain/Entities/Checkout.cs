namespace Walos.Domain.Entities;

public sealed record CheckoutCommand
{
    public long CompanyId { get; init; }
    public long BranchId { get; init; }
    public long UserId { get; init; }
    public long TableId { get; init; }
    public string DiscountType { get; init; } = "none";
    public decimal DiscountValue { get; init; }
    public decimal SubmittedFinalTotal { get; init; }
    public int SplitCount { get; init; } = 1;
    public bool OverrideConfirmed { get; init; }
    public bool HasCredit { get; init; }
    public decimal CreditAmountPaid { get; init; }
    public string? CreditCustomerName { get; init; }
    public string? CreditNotes { get; init; }
    public decimal TipAmount { get; init; }
    public bool TipIncluded { get; init; }
    public IReadOnlyList<CheckoutPayment> Payments { get; init; } = [];
}

public sealed record CheckoutPayment
{
    public CheckoutPayment()
    {
    }

    public CheckoutPayment(string method, decimal amount, string? reference)
    {
        Method = method;
        Amount = amount;
        Reference = reference;
    }

    public string Method { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
}

public sealed class CheckoutResult
{
    public long OrderId { get; init; }
    public int TableNumber { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public string DiscountType { get; init; } = "none";
    public decimal DiscountValue { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal TipAmount { get; init; }
    public int SplitCount { get; init; }
    public List<OrderItem> Items { get; init; } = [];
    public List<CheckoutPayment> Payments { get; init; } = [];
    public DateTime InvoicedAt { get; init; }
    public long? CreditId { get; init; }
    public decimal? CreditAmount { get; init; }
    public bool IsReplay { get; init; }
}

public sealed record AddOrderItemsCommand
{
    public long CompanyId { get; init; }
    public long BranchId { get; init; }
    public long TableId { get; init; }
    public IReadOnlyList<OrderItem> Items { get; init; } = [];
}

public sealed record UpdateOrderItemQuantityCommand
{
    public long CompanyId { get; init; }
    public long BranchId { get; init; }
    public long OrderItemId { get; init; }
    public decimal Quantity { get; init; }
}
