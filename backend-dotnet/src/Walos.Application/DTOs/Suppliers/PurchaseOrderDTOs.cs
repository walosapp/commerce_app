namespace Walos.Application.DTOs.Suppliers;

public class CreatePurchaseOrderRequest
{
    public long SupplierId { get; set; }
    public long BranchId { get; set; }
    public string? Notes { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = [];
}

public class PurchaseOrderItemDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class ReceivePurchaseOrderRequest
{
    public string? Notes { get; set; }
    public List<ReceiveItemDto> Items { get; set; } = [];
}

public class ReceiveItemDto
{
    public long OrderItemId { get; set; }
    public decimal ReceivedQty { get; set; }
}

public class PurchaseOrderResponse
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PurchaseOrderItemResponse> Items { get; set; } = [];
}

public class PurchaseOrderItemResponse
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Subtotal { get; set; }
    public decimal? ReceivedQty { get; set; }
}
