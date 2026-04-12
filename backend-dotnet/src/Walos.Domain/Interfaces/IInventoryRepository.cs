using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IInventoryRepository
{
    Task<IEnumerable<Product>> GetAllProductsAsync(long companyId, ProductFilter? filters = null);
    Task<Product?> GetProductByIdAsync(long productId, long companyId);
    Task<Product> CreateProductAsync(Product product);
    Task<IEnumerable<Stock>> GetStockByBranchAsync(long branchId, long companyId);
    Task<Stock> UpdateStockAsync(long branchId, long productId, decimal quantity, long companyId);
    Task<Movement> CreateMovementAsync(Movement movement);
    Task<AiInteraction> SaveAiInteractionAsync(AiInteraction interaction);
    Task<AiInteraction?> GetAiInteractionByIdAsync(long id, long companyId);
    Task UpdateAiInteractionStatusAsync(long id, string status, bool confirmedByUser, long companyId);
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(long companyId, long? branchId = null);
    Task<IEnumerable<Product>> FindProductsByNameAsync(long companyId, string name);
    Task<IEnumerable<ProfitReportRow>> GetProductProfitsAsync(long companyId, long branchId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<CategoryInfo>> GetCategoriesAsync(long companyId);
    Task<IEnumerable<UnitInfo>> GetUnitsAsync(long companyId);
    Task<Stock> CreateStockEntryAsync(long branchId, long productId, decimal quantity, long companyId);
    Task<IEnumerable<AiInteraction>> GetAiInteractionsBySessionAsync(string sessionId, long companyId);
    Task UpdateProductCostAndPriceAsync(long productId, long companyId, decimal newCostPrice, decimal? newSalePrice = null);
    Task<Stock?> GetStockByProductAsync(long branchId, long productId, long companyId);
    Task<Product> UpdateProductAsync(Product product);
    Task UpdateProductImageAsync(long productId, long companyId, string imageUrl);
    Task SoftDeleteProductAsync(long productId, long companyId, long userId);
}

public class ProfitReportRow
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

public class ProductFilter
{
    public long? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}

public class CategoryInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UnitInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
}
