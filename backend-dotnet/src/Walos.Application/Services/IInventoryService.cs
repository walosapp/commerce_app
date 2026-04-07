using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface IInventoryService
{
    Task<AiProcessResult> ProcessAiInventoryInputAsync(string userInput, AiInputContext context);
    Task<AiConfirmResult> ConfirmAiActionAsync(long interactionId, long userId, long companyId);
    Task<IEnumerable<Stock>> GetLowStockProductsAsync(long companyId, long branchId);
    Task<IEnumerable<ProfitReport>> CalculateProductProfitsAsync(long companyId, long branchId, DateRange? dateRange = null);
}

public class AiInputContext
{
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public long UserId { get; set; }
    public string InputType { get; set; } = "text";
    public string? SessionId { get; set; }
    public string? CompanyName { get; set; }
    public string? BranchName { get; set; }
}

public class AiProcessResult
{
    public long InteractionId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public object? Data { get; set; }
    public int Confidence { get; set; }
    public bool RequiresConfirmation { get; set; }
}

public class AiConfirmResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<Movement> Movements { get; set; } = new();
}

public class ProfitReport
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MarginPercentage { get; set; }
    public int TotalSales { get; set; }
    public decimal TotalQuantitySold { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
}

public class DateRange
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
