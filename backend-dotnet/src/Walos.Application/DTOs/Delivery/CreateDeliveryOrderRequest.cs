namespace Walos.Application.DTOs.Delivery;

public class CreateDeliveryOrderRequest
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? Notes { get; set; }
    public decimal DeliveryFee { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public string Source { get; set; } = "manual";
    public List<DeliveryOrderItemRequest> Items { get; set; } = new();
}

public class DeliveryOrderItemRequest
{
    public long ProductId { get; set; }
    // Legacy compatibility only. The backend persists the canonical product snapshot.
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    // Legacy compatibility only. The backend loads sale_price from inventory.products.
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}
