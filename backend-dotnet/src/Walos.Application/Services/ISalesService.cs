using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface ISalesService
{
    Task<long?> ResolveBranchAsync(long companyId, long? tenantBranchId, long? requestedBranchId, bool required = false);
    Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId);
    Task<CreateTableResult> CreateTableAsync(long companyId, long branchId, long userId, CreateTableRequest request);
    Task<InvoiceResult> InvoiceTableAsync(long companyId, long branchId, long userId, long tableId, InvoiceTableRequest request);
    Task CancelTableAsync(long companyId, long tableId);
    Task CancelTableAsync(long companyId, long? branchId, long tableId);
    Task UpdateItemQuantityAsync(long companyId, long branchId, long itemId, UpdateItemQuantityRequest request);
    Task AddItemsToTableAsync(long companyId, long branchId, long tableId, List<CreateTableItemDto> items);
    Task RenameTableAsync(long companyId, long tableId, string name);
    Task RenameTableAsync(long companyId, long? branchId, long tableId, string name);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long companyId, long? branchId, long orderId);
    Task<ReceiptData> GetReceiptAsync(long companyId, long orderId);
    Task<ReceiptData> GetReceiptAsync(long companyId, long? branchId, long orderId);
    Task<KitchenTicketData> GetKitchenTicketAsync(long companyId, long orderId);
    Task<KitchenTicketData> GetKitchenTicketAsync(long companyId, long? branchId, long orderId);
    Task<(List<OrderDetailResponse> Items, int TotalCount)> SearchOrdersAsync(long companyId, OrderSearchRequest request);
    Task<byte[]> ExportOrdersCsvAsync(long companyId, OrderSearchRequest request);
    Task<OrderDetailResponse> GetOrderDetailAsync(long companyId, long orderId);
    Task<OrderDetailResponse> GetOrderDetailAsync(long companyId, long? branchId, long orderId);
}

public class CreateTableResult
{
    public SalesTable Table { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
}

public class InvoiceResult
{
    public long OrderId { get; set; }
    public int TableNumber { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public string DiscountType { get; set; } = "none";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public decimal FinalTotalPaid { get; set; }
    public decimal TipAmount { get; set; }
    public int SplitCount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public List<PaymentLineDto> Payments { get; set; } = new();
    public DateTime InvoicedAt { get; set; }
    public long? CreditId { get; set; }
    public decimal? CreditAmount { get; set; }
    public bool IsReplay { get; set; }
}
