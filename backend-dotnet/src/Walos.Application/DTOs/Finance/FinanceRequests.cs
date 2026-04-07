namespace Walos.Application.DTOs.Finance;

public class CreateFinancialCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "expense";
    public string? ColorHex { get; set; }
}

public class UpdateFinancialCategoryRequest : CreateFinancialCategoryRequest
{
}

public class CreateFinancialEntryRequest
{
    public long? BranchId { get; set; }
    public long CategoryId { get; set; }
    public string Type { get; set; } = "expense";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime EntryDate { get; set; }
    public string? Nature { get; set; }
    public string? Frequency { get; set; }
    public string? Notes { get; set; }
}

public class UpdateFinancialEntryRequest : CreateFinancialEntryRequest
{
}
