namespace Walos.Domain.Entities;

public class SalesSummary
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalDiscounts { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageTicket { get; set; }
    public decimal TotalCredits { get; set; }
    public int CreditOrders { get; set; }
    public List<TopProduct> TopProducts { get; set; } = new();
    public List<HourlySale> HourlySales { get; set; } = new();
}

public class TopProduct
{
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class HourlySale
{
    public int Hour { get; set; }
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
}

public class OrderItem
{
    public long Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public string? Notes { get; set; }
}

public class CompletedOrder
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public int TableNumber { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotalPaid { get; set; }
    public int SplitReferenceCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasCredit { get; set; }
}
