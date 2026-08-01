namespace Walos.Application.DTOs.PosDeli;

public class PosDeliSaleRequest
{
    public List<PosDeliSaleItem> Items { get; set; } = [];
    public List<PosDeliPayment> Payments { get; set; } = [];
    public decimal? CashReceived { get; set; }
    public string? Notes { get; set; }
}

public class PosDeliSaleItem
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsWeighed { get; set; }
}

public class PosDeliPayment
{
    public string Method { get; set; } = "cash";
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}

public class PosDeliSaleResponse
{
    public long SaleId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Change { get; set; }
}

public class PosDeliProductResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public bool TrackStock { get; set; }
    public string? ImageUrl { get; set; }
    public string? CategoryName { get; set; }
    public string? UnitName { get; set; }
    public string? UnitAbbreviation { get; set; }
    public decimal Quantity { get; set; }
    public decimal AvailableQuantity { get; set; }
}
