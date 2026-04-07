namespace Walos.Domain.Entities;

public class FinancialCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "expense";
    public string? ColorHex { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public int EntryCount { get; set; }
    public decimal TotalAmount { get; set; }
}
