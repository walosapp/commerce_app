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
    Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long orderId, long companyId);
    Task<OrderItem?> GetOrderItemByIdAsync(long itemId, long companyId);
    Task UpdateTableStatusAsync(long tableId, long companyId, string status);
    Task UpdateOrderStatusAsync(long orderId, long companyId, string status);
    Task<int> GetNextTableNumberAsync(long companyId, long branchId);
    Task UpdateOrderItemQuantityAsync(long orderItemId, long companyId, decimal quantity);
    Task DeleteOrderItemAsync(long orderItemId, long companyId);
    Task AddOrderItemAsync(OrderItem item);
    Task RecalculateOrderTotalAsync(long orderId, long companyId);
    Task UpdateOrderInvoiceSummaryAsync(long orderId, long companyId, string? discountType, decimal discountValue, decimal discountAmount, decimal finalTotalPaid, int splitReferenceCount);
    Task RenameTableAsync(long tableId, long companyId, string name);
}
