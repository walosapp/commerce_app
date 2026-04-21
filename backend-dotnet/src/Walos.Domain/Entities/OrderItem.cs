namespace Walos.Domain.Entities;

public class OrderItem
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation (from JOINs)
    public string? ImageUrl { get; set; }
}
