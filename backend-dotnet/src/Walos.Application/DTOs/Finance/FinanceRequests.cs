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
    public string? Status { get; set; }
    public int? OccurrenceInMonth { get; set; }
    public bool? IsManual { get; set; }
}

public class UpdateFinancialEntryRequest : CreateFinancialEntryRequest
{
}

public class CreateFinancialRecurringTemplateRequest
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
}

public class UpdateFinancialRecurringTemplateRequest : CreateFinancialRecurringTemplateRequest
{
}

public class InitFinanceMonthRequest
{
    public long? BranchId { get; set; }
    public string Month { get; set; } = string.Empty;
}
