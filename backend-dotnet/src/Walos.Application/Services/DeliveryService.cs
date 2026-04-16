using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Delivery;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Application.Services;

public class DeliveryService : IDeliveryService
{
    private readonly IDeliveryRepository _repo;
    private readonly ILogger<DeliveryService> _logger;

    private static readonly Dictionary<string, string[]> ValidTransitions = new()
    {
        ["new"]                 = ["accepted", "rejected", "cancelled"],
        ["accepted"]            = ["preparing", "rejected", "cancelled"],
        ["preparing"]           = ["ready_for_dispatch", "cancelled"],
        ["ready_for_dispatch"]  = ["out_for_delivery", "cancelled"],
        ["out_for_delivery"]    = ["delivered", "returned"],
        ["delivered"]           = [],
        ["rejected"]            = [],
        ["cancelled"]           = [],
        ["returned"]            = [],
    };

    private static readonly string[] RequireComment = ["rejected", "cancelled", "returned"];

    public DeliveryService(IDeliveryRepository repo, ILogger<DeliveryService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<IEnumerable<DeliveryOrder>> GetOrdersAsync(
        long companyId, long branchId, string? status, DateTime? dateFrom, DateTime? dateTo)
        => _repo.GetOrdersAsync(companyId, branchId, status, dateFrom, dateTo);

    public Task<DeliveryOrder?> GetOrderByIdAsync(long orderId, long companyId)
        => _repo.GetOrderByIdAsync(orderId, companyId);

    public async Task<DeliveryOrder> CreateOrderAsync(
        long companyId, long branchId, long userId, CreateDeliveryOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
            throw new ValidationException("El pedido debe tener al menos un item");

        var orderNumber = await _repo.GetNextOrderNumberAsync(companyId, branchId);
        var subtotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var total = subtotal + request.DeliveryFee - request.DiscountAmount;

        var order = new DeliveryOrder
        {
            CompanyId = companyId,
            BranchId = branchId,
            Source = request.Source,
            OrderNumber = orderNumber,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerAddress = request.CustomerAddress,
            Notes = request.Notes,
            Subtotal = subtotal,
            DeliveryFee = request.DeliveryFee,
            DiscountAmount = request.DiscountAmount,
            Total = total,
            CreatedBy = userId,
        };

        var items = request.Items.Select(i => new DeliveryOrderItem
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Notes = i.Notes,
        }).ToList();

        return await _repo.CreateOrderAsync(order, items);
    }

    public Task AcceptOrderAsync(long orderId, long companyId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, userId, "accepted", comment,
            new() { ["accepted_at"] = DateTime.UtcNow });

    public Task RejectOrderAsync(long orderId, long companyId, long userId, string comment)
        => TransitionAsync(orderId, companyId, userId, "rejected", comment,
            new() { ["rejected_reason"] = null });

    public Task PrepareOrderAsync(long orderId, long companyId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, userId, "preparing", comment, new());

    public Task ReadyOrderAsync(long orderId, long companyId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, userId, "ready_for_dispatch", comment,
            new() { ["prepared_at"] = DateTime.UtcNow });

    public Task DispatchOrderAsync(long orderId, long companyId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, userId, "out_for_delivery", comment,
            new() { ["dispatched_at"] = DateTime.UtcNow });

    public Task DeliverOrderAsync(long orderId, long companyId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, userId, "delivered", comment,
            new() { ["delivered_at"] = DateTime.UtcNow });

    public Task CancelOrderAsync(long orderId, long companyId, long userId, string comment)
        => TransitionAsync(orderId, companyId, userId, "cancelled", comment, new());

    public Task ReturnOrderAsync(long orderId, long companyId, long userId, string comment)
        => TransitionAsync(orderId, companyId, userId, "returned", comment,
            new() { ["returned_reason"] = null });

    private async Task TransitionAsync(long orderId, long companyId, long userId,
        string newStatus, string? comment, Dictionary<string, DateTime?> timestamps)
    {
        if (RequireComment.Contains(newStatus) && string.IsNullOrWhiteSpace(comment))
            throw new ValidationException($"Se requiere un comentario para el estado '{newStatus}'");

        var order = await _repo.GetOrderByIdAsync(orderId, companyId)
            ?? throw new BusinessException("Pedido no encontrado");

        if (!ValidTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new BusinessException($"No se puede pasar de '{order.Status}' a '{newStatus}'");

        await _repo.UpdateOrderStatusAsync(orderId, companyId, newStatus, comment, userId, timestamps);
        _logger.LogInformation("Pedido {OrderId} → {Status} por user {UserId}", orderId, newStatus, userId);
    }
}
