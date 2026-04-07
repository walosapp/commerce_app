namespace Walos.Domain.Interfaces;

public interface IAiService
{
    Task<AiInventoryResponse> ProcessInventoryInputAsync(string userInput, AiContext context, List<AiConversationMessage>? history = null);
    Task<string> GenerateAlertSuggestionAsync(AlertData alert);
    Task<object> AnalyzeSalesTrendsAsync(object salesData);
}

public class AiConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class AiInventoryResponse
{
    public string Action { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Response { get; set; } = string.Empty;
    public AiInventoryData? Data { get; set; }
    public AiMetadata? Metadata { get; set; }
}

public class AiInventoryData
{
    public List<AiProductEntry> Products { get; set; } = new();
    public decimal? Total { get; set; }
}

public class AiProductEntry
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal SalePrice { get; set; }
    public decimal ProfitMargin { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal MinStock { get; set; }
    public string? Description { get; set; }
    public bool IsNew { get; set; }
}

public class AiMetadata
{
    public string Model { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
}

public class AiContext
{
    public string? CompanyName { get; set; }
    public string? BranchName { get; set; }
    public int ExistingProductsCount { get; set; }
    public List<string> ExistingProductNames { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Units { get; set; } = new();
}

public class AlertData
{
    public string Type { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
}
