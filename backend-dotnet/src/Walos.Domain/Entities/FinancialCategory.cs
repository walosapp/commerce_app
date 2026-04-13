namespace Walos.Domain.Entities;

public class FinancialCategory : BaseEntity
{
    public long? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "expense";
    public string? ColorHex { get; set; }
    public decimal DefaultAmount { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public string Nature { get; set; } = "fixed";
    public string Frequency { get; set; } = "monthly";
    public int? BiweeklyDay1 { get; set; }
    public int? BiweeklyDay2 { get; set; }
    public bool AutoIncludeInMonth { get; set; } = true;
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public int EntryCount { get; set; }
    public decimal TotalAmount { get; set; }
}
