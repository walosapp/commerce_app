namespace Walos.Domain.Entities;

public class Credit : BaseEntity
{
    public long BranchId { get; set; }
    public long? OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public decimal OriginalTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal CreditAmount { get; set; }
    public string Status { get; set; } = "pending"; // pending | partial | paid | cancelled
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }
    public long? CreatedBy { get; set; }

    // Navigation
    public List<CreditPayment>? Payments { get; set; }
}

public class CreditPayment
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long CreditId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public long? CashRegisterId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}

public sealed class CreditPaymentCommand
{
    public long CreditId { get; init; }
    public long CompanyId { get; init; }
    public long BranchId { get; init; }
    public long UserId { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
