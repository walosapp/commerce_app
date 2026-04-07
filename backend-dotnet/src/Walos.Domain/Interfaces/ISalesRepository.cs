using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ISalesRepository
{
    Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId);
    Task<SalesTable?> GetTableByIdAsync(long tableId, long companyId);
    Task<SalesTable> CreateTableAsync(SalesTable table);
    Task<Order> CreateOrderAsync(Order order, List<OrderItem> items);
    Task<Order?> GetOrderByIdAsync(long orderId, long companyId);
    Task<Order?> GetOrderByTableIdAsync(long tableId, long companyId);
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId);
    Task<OrderItem?> GetOrderItemByIdAsync(long itemId);
    Task UpdateTableStatusAsync(long tableId, long companyId, string status);
    Task UpdateOrderStatusAsync(long orderId, string status);
    Task<int> GetNextTableNumberAsync(long companyId, long branchId);
    Task UpdateOrderItemQuantityAsync(long orderItemId, decimal quantity);
    Task DeleteOrderItemAsync(long orderItemId);
    Task AddOrderItemAsync(OrderItem item);
    Task RecalculateOrderTotalAsync(long orderId);
    Task UpdateOrderInvoiceSummaryAsync(long orderId, string? discountType, decimal discountValue, decimal discountAmount, decimal finalTotalPaid, int splitReferenceCount);
}
