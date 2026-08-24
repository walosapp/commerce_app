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
        ["new"] = ["accepted", "rejected", "cancelled"],
        ["accepted"] = ["preparing", "rejected", "cancelled"],
        ["preparing"] = ["ready_for_dispatch", "cancelled"],
        ["ready_for_dispatch"] = ["out_for_delivery", "cancelled"],
        ["out_for_delivery"] = ["delivered", "returned"],
        ["delivered"] = [],
        ["rejected"] = [],
        ["cancelled"] = [],
        ["returned"] = [],
    };

    private static readonly string[] RequireComment = ["rejected", "cancelled", "returned"];
    private static readonly HashSet<string> ValidSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "manual", "whatsapp", "web", "rappi", "didi_food", "uber_eats"
    };

    public DeliveryService(IDeliveryRepository repo, ILogger<DeliveryService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<IEnumerable<DeliveryOrder>> GetOrdersAsync(
        long companyId, long branchId, string? status, DateTime? dateFrom, DateTime? dateTo)
        => _repo.GetOrdersAsync(companyId, branchId, status, dateFrom, dateTo);

    public Task<DeliveryOrder?> GetOrderByIdAsync(long orderId, long companyId, long branchId)
        => _repo.GetOrderByIdAsync(orderId, companyId, branchId);

    public async Task<DeliveryOrder> CreateOrderAsync(
        long companyId, long branchId, long userId, CreateDeliveryOrderRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new ValidationException("El pedido debe tener al menos un item.");

        if (companyId <= 0 || branchId <= 0 || userId <= 0)
            throw new ValidationException("El pedido requiere un comercio, sucursal y usuario autenticados.");

        var source = request.Source?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidSources.Contains(source))
            throw new ValidationException("El origen del pedido no es valido.");

        var deliveryFee = Math.Round(request.DeliveryFee, 2, MidpointRounding.AwayFromZero);
        var discountAmount = Math.Round(request.DiscountAmount, 2, MidpointRounding.AwayFromZero);
        if (deliveryFee < 0 || discountAmount < 0)
            throw new ValidationException("El costo de domicilio y el descuento no pueden ser negativos.");

        if (request.Items.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
            throw new ValidationException("Todos los items deben tener producto y cantidad positiva.");

        var items = request.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new DeliveryOrderItem
            {
                ProductId = group.Key,
                Quantity = Math.Round(group.Sum(item => item.Quantity), 2, MidpointRounding.AwayFromZero),
                Notes = NormalizeOptional(
                    group.Select(item => item.Notes).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    500)
            })
            .ToList();

        if (items.Any(item => item.Quantity <= 0))
            throw new ValidationException("Todos los items deben tener una cantidad valida para la precision admitida.");

        var order = new DeliveryOrder
        {
            CompanyId = companyId,
            BranchId = branchId,
            Source = source,
            CustomerName = NormalizeOptional(request.CustomerName, 200),
            CustomerPhone = NormalizeOptional(request.CustomerPhone, 30),
            CustomerAddress = NormalizeOptional(request.CustomerAddress, 500),
            Notes = NormalizeOptional(request.Notes, 4000),
            DeliveryFee = deliveryFee,
            DiscountAmount = discountAmount,
            CreatedBy = userId,
        };

        return await _repo.CreateOrderAsync(order, items);
    }

    public Task AcceptOrderAsync(long orderId, long companyId, long branchId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "accepted", comment,
            new() { ["accepted_at"] = DateTime.UtcNow });

    public Task RejectOrderAsync(long orderId, long companyId, long branchId, long userId, string comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "rejected", comment,
            new() { ["rejected_reason"] = null });

    public Task PrepareOrderAsync(long orderId, long companyId, long branchId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "preparing", comment, new());

    public Task ReadyOrderAsync(long orderId, long companyId, long branchId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "ready_for_dispatch", comment,
            new() { ["prepared_at"] = DateTime.UtcNow });

    public Task DispatchOrderAsync(long orderId, long companyId, long branchId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "out_for_delivery", comment,
            new() { ["dispatched_at"] = DateTime.UtcNow });

    public Task DeliverOrderAsync(long orderId, long companyId, long branchId, long userId, string? comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "delivered", comment,
            new() { ["delivered_at"] = DateTime.UtcNow });

    public Task CancelOrderAsync(long orderId, long companyId, long branchId, long userId, string comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "cancelled", comment, new());

    public Task ReturnOrderAsync(long orderId, long companyId, long branchId, long userId, string comment)
        => TransitionAsync(orderId, companyId, branchId, userId, "returned", comment,
            new() { ["returned_reason"] = null });

    private async Task TransitionAsync(
        long orderId,
        long companyId,
        long branchId,
        long userId,
        string newStatus,
        string? comment,
        Dictionary<string, DateTime?> timestamps)
    {
        if (RequireComment.Contains(newStatus) && string.IsNullOrWhiteSpace(comment))
            throw new ValidationException($"Se requiere un comentario para el estado '{newStatus}'.");

        var order = await _repo.GetOrderByIdAsync(orderId, companyId, branchId)
            ?? throw new BusinessException("Pedido no encontrado.");

        if (!ValidTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new BusinessException($"No se puede pasar de '{order.Status}' a '{newStatus}'.");

        var updated = await _repo.UpdateOrderStatusAsync(
            orderId,
            companyId,
            branchId,
            order.Status,
            newStatus,
            comment?.Trim(),
            userId,
            timestamps);

        if (!updated)
            throw new BusinessException("El pedido cambio de estado durante la operacion. Actualiza e intenta nuevamente.");

        _logger.LogInformation("Pedido {OrderId} -> {Status} por user {UserId}", orderId, newStatus, userId);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ValidationException($"Un campo del pedido supera la longitud maxima de {maxLength} caracteres.");

        return normalized;
    }
}
