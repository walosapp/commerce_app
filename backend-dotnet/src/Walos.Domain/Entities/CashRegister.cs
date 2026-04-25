namespace Walos.Domain.Entities;

public class CashRegister : BaseEntity
{
    public long BranchId { get; set; }
    public long OpenedBy { get; set; }
    public long? ClosedBy { get; set; }

    public string Status { get; set; } = "open"; // open | closed

    public decimal OpeningAmount { get; set; }
    public decimal? ClosingAmount { get; set; }

    public decimal? ExpectedCash { get; set; }
    public decimal? Difference { get; set; }

    public decimal TotalSales { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalTransferSales { get; set; }
    public decimal TotalOtherSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalTips { get; set; }

    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public int OrderCount { get; set; }

    public string? Notes { get; set; }

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Joined fields
    public string? OpenedByName { get; set; }
    public string? ClosedByName { get; set; }
}

public class CashMovement : BaseEntity
{
    public long CashRegisterId { get; set; }
    public string Type { get; set; } = "in"; // in | out
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public long CreatedBy { get; set; }

    // Joined field
    public string? CreatedByName { get; set; }
}
