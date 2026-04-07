namespace Walos.Application.DTOs.Sales;

public class CreateTableRequest
{
    public string? Name { get; set; }
    public List<CreateTableItemDto> Items { get; set; } = new();
}

public class CreateTableItemDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class InvoiceTableRequest
{
    public string DiscountType { get; set; } = "none";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotalPaid { get; set; }
    public int SplitCount { get; set; } = 1;
    public bool OverrideConfirmed { get; set; }
}
