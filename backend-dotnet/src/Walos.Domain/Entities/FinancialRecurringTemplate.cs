namespace Walos.Domain.Entities;

public class FinancialRecurringTemplate : BaseEntity
{
    public long? BranchId { get; set; }
    public long CategoryId { get; set; }
    public string Type { get; set; } = "expense";
    public string Description { get; set; } = string.Empty;
    public decimal DefaultAmount { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public string Nature { get; set; } = "fixed";
    public string Frequency { get; set; } = "monthly";
    public int? BiweeklyDay1 { get; set; }
    public int? BiweeklyDay2 { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public string? CategoryName { get; set; }
}
