namespace Walos.Application.DTOs.Suppliers;

public class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSupplierRequest : CreateSupplierRequest { }

public class AddSupplierProductRequest
{
    public long ProductId { get; set; }
    public string? SupplierSku { get; set; }
    public decimal? UnitCost { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Notes { get; set; }
}

public class SuggestedOrderItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal SuggestedQty { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? EstimatedCost { get; set; }
}

public class SuggestedOrderResponse
{
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public List<SuggestedOrderItem> Items { get; set; } = new();
    public decimal TotalEstimatedCost { get; set; }
}
