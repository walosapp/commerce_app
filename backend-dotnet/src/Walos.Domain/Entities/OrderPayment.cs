namespace Walos.Domain.Entities;

public class OrderPayment : BaseEntity
{
    public long OrderId { get; set; }
    public string Method { get; set; } = "cash"; // cash | card | transfer | nequi | other
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentMethodSummary
{
    public string Method { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}
