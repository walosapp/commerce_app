namespace Walos.Domain.Entities;

public class FinancialEntry : BaseEntity
{
    public long? BranchId { get; set; }
    public long CategoryId { get; set; }
    public long? FinancialItemId { get; set; }
    public string Status { get; set; } = "posted";
    public int OccurrenceInMonth { get; set; } = 1;
    public bool IsManual { get; set; } = true;
    public string Type { get; set; } = "expense";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime EntryDate { get; set; }
    public string? Nature { get; set; }
    public string? Frequency { get; set; }
    public string? Notes { get; set; }
    public long? CreatedBy { get; set; }
    public string? CategoryName { get; set; }
    public string? BranchName { get; set; }
}
