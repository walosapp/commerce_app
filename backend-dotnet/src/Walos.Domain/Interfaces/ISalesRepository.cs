using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ISalesRepository
{
    Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId);
    Task<SalesTable?> GetTableByIdAsync(long tableId, long companyId);
    Task<SalesTable?> GetTableByIdAsync(long tableId, long companyId, long? branchId);
    Task<SalesTable> CreateTableAsync(SalesTable table);
    Task<Order> CreateOrderAsync(Order order, List<OrderItem> items);
    Task<Order?> GetOrderByIdAsync(long orderId, long companyId);
    Task<Order?> GetOrderByIdAsync(long orderId, long companyId, long? branchId);
    Task<Order?> GetOrderByTableIdAsync(long tableId, long companyId);
    Task<Order?> GetOrderByTableIdAsync(long tableId, long companyId, long? branchId);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId, long companyId);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId, long companyId, long? branchId);
    Task<OrderItem?> GetOrderItemByIdAsync(long itemId, long companyId);
    Task<OrderItem?> GetOrderItemByIdAsync(long itemId, long companyId, long? branchId);
    Task UpdateTableStatusAsync(long tableId, long companyId, string status);
    Task UpdateTableStatusAsync(long tableId, long companyId, long? branchId, string status);
    Task UpdateOrderStatusAsync(long orderId, long companyId, string status);
    Task UpdateOrderStatusAsync(long orderId, long companyId, long? branchId, string status);
    Task<bool> CancelActiveTableAsync(long tableId, long companyId, long? branchId);
    Task<int> GetNextTableNumberAsync(long companyId, long branchId);
    Task UpdateOrderItemQuantityAsync(long orderItemId, long companyId, decimal quantity);
    Task DeleteOrderItemAsync(long orderItemId, long companyId);
    Task AddOrderItemAsync(OrderItem item);
    Task RecalculateOrderTotalAsync(long orderId, long companyId);
    Task UpdateOrderInvoiceSummaryAsync(long orderId, long companyId, string? discountType, decimal discountValue, decimal discountAmount, decimal finalTotalPaid, int splitReferenceCount, long? cashRegisterId, string? paymentMethod, decimal tipAmount, bool tipIncluded);
    Task RenameTableAsync(long tableId, long companyId, string name);
    Task RenameTableAsync(long tableId, long companyId, long? branchId, string name);
    Task<SalesSummary> GetSalesSummaryAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo);
    Task<IEnumerable<CompletedOrder>> GetCompletedOrdersAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo);
    Task<IEnumerable<Order>> SearchOrdersAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo,
        string? status, string? refundStatus, string? paymentMethod, string? search,
        decimal? minTotal, decimal? maxTotal, string sortBy, string sortDir, int offset, int limit);
    Task<int> SearchOrdersCountAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo,
        string? status, string? refundStatus, string? paymentMethod, string? search,
        decimal? minTotal, decimal? maxTotal);
}
