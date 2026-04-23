namespace Walos.Domain.Entities.Platform;

public class BillingInvoice
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; } = 19;
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? SentAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentRef { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? CompanyName { get; set; }
    public List<BillingInvoiceItem> Items { get; set; } = new();
}

public class BillingInvoiceItem
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public string? ServiceCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public DateTime CreatedAt { get; set; }
}
