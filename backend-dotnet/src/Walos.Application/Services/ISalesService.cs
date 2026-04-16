using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface ISalesService
{
    Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId);
    Task<CreateTableResult> CreateTableAsync(long companyId, long branchId, long userId, CreateTableRequest request);
    Task<InvoiceResult> InvoiceTableAsync(long companyId, long branchId, long userId, long tableId, InvoiceTableRequest request);
    Task CancelTableAsync(long companyId, long tableId);
    Task UpdateItemQuantityAsync(long companyId, long branchId, long itemId, UpdateItemQuantityRequest request);
    Task AddItemsToTableAsync(long companyId, long tableId, List<CreateTableItemDto> items);
}

public class CreateTableResult
{
    public SalesTable Table { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
}

public class InvoiceResult
{
    public int TableNumber { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public string DiscountType { get; set; } = "none";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public decimal FinalTotalPaid { get; set; }
    public int SplitCount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public DateTime InvoicedAt { get; set; }
}
