using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface IRefundService
{
    Task<RefundResponse> CreateRefundAsync(long companyId, long branchId, long userId, string idempotencyKey, CreateRefundRequest request);
    Task<RefundResponse?> GetByIdAsync(long id, long companyId, long branchId);
    Task<IEnumerable<RefundResponse>> GetByOrderIdAsync(long orderId, long companyId, long branchId);
    Task<(IEnumerable<RefundResponse> Items, int TotalCount)> GetAllAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit);
}

public class RefundService : IRefundService
{
    private readonly IRefundRepository _refundRepo;
    private readonly ILogger<RefundService> _logger;

    public RefundService(IRefundRepository refundRepo, ILogger<RefundService> logger)
    {
        _refundRepo = refundRepo;
        _logger = logger;
    }

    public async Task<RefundResponse> CreateRefundAsync(
        long companyId,
        long branchId,
        long userId,
        string idempotencyKey,
        CreateRefundRequest request)
    {
        var refundType = request.RefundType?.Trim().ToLowerInvariant();
        if (refundType is not ("full" or "partial"))
            throw new ValidationException("Tipo de devolucion invalido. Use 'full' o 'partial'.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 500)
            throw new ValidationException("El motivo de la devolucion debe tener entre 10 y 500 caracteres.");

        idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        if (idempotencyKey.Length is < 8 or > 100)
            throw new ValidationException("Idempotency-Key es obligatorio y debe tener entre 8 y 100 caracteres.");

        var items = request.Items?.Select(i => new RefundProcessItem(i.OrderItemId, i.Quantity)).ToList() ?? [];
        if (refundType == "partial" && items.Count == 0)
            throw new ValidationException("Debe especificar los items a devolver en una devolucion parcial.");

        if (refundType == "full" && items.Count > 0)
            throw new ValidationException("Una devolucion total no debe especificar items.");

        if (items.Any(i => i.OrderItemId <= 0 || i.Quantity <= 0))
            throw new ValidationException("Todos los items deben tener id y cantidad positiva.");

        if (items.GroupBy(i => i.OrderItemId).Any(g => g.Count() > 1))
            throw new ValidationException("No se puede repetir el mismo item en una devolucion.");

        var fingerprint = CreateFingerprint(request.OrderId, refundType, reason, items);
        var result = await _refundRepo.ProcessAsync(new RefundProcessCommand
        {
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            OrderId = request.OrderId,
            RefundType = refundType,
            Reason = reason,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = fingerprint,
            Items = items
        });

        _logger.LogInformation(
            "Devolucion {RefundId} procesada para orden {OrderId}. Replay={Replayed}",
            result.Refund.Id, request.OrderId, result.Replayed);

        return MapToResponse(result.Refund, result.Items);
    }

    public async Task<RefundResponse?> GetByIdAsync(long id, long companyId, long branchId)
    {
        var refund = await _refundRepo.GetByIdAsync(id, companyId, branchId);
        return refund != null ? MapToResponse(refund, refund.Items ?? []) : null;
    }

    public async Task<IEnumerable<RefundResponse>> GetByOrderIdAsync(long orderId, long companyId, long branchId)
    {
        var refunds = await _refundRepo.GetByOrderIdAsync(orderId, companyId, branchId);
        return refunds.Select(r => MapToResponse(r, r.Items ?? []));
    }

    public async Task<(IEnumerable<RefundResponse> Items, int TotalCount)> GetAllAsync(
        long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit)
    {
        var items = await _refundRepo.GetAllAsync(companyId, branchId, dateFrom, dateTo, page, limit);
        var count = await _refundRepo.GetCountAsync(companyId, branchId, dateFrom, dateTo);
        return (items.Select(r => MapToResponse(r, r.Items ?? [])), count);
    }

    private static string CreateFingerprint(
        long orderId,
        string refundType,
        string reason,
        IEnumerable<RefundProcessItem> items)
    {
        var canonicalItems = string.Join(",", items
            .OrderBy(i => i.OrderItemId)
            .Select(i => $"{i.OrderItemId}:{i.Quantity.ToString("0.##################", CultureInfo.InvariantCulture)}"));
        var canonical = $"{orderId}|{refundType}|{reason}|{canonicalItems}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static RefundResponse MapToResponse(Refund r, List<RefundItem> items) => new(
        r.Id, r.CompanyId, r.BranchId, r.OrderId,
        "", r.RefundType, r.RefundAmount, r.Reason, r.Status,
        null, "", r.CreatedAt,
        items.Select(i => new RefundItemResponse(
            i.Id, i.OrderItemId, "", i.Quantity, i.UnitPrice, i.Subtotal)).ToList());
}
