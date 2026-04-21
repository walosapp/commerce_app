namespace Walos.Application.DTOs.Sales;

public class CreditResponse
{
    public long Id { get; set; }
    public long? OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? OrderNumber { get; set; }
    public decimal OriginalTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal CreditAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<CreditPaymentResponse>? Payments { get; set; }
}

public class CreditPaymentResponse
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddCreditPaymentRequest
{
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
