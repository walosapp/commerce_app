using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface ICashRegisterService
{
    Task<CashRegisterResponse> OpenAsync(long companyId, long branchId, long userId, OpenCashRegisterRequest request);
    Task<CashRegisterResponse?> GetActiveAsync(long companyId, long branchId, long userId);
    Task<CashRegisterResponse> CloseAsync(long id, long companyId, long branchId, long userId, CloseCashRegisterRequest request);
    Task<CashMovementResponse> AddMovementAsync(long cashRegisterId, long companyId, long branchId, long userId, CashMovementRequest request);
    Task<IEnumerable<CashMovementResponse>> GetMovementsAsync(long cashRegisterId, long companyId, long branchId);
    Task<CashRegisterSummaryResponse> GetSummaryAsync(long id, long companyId, long branchId);
    Task<(IEnumerable<CashRegisterResponse> Items, int TotalCount)> GetHistoryAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit);
    Task UpdateTotalsFromOrderAsync(long cashRegisterId, long companyId, decimal totalSales, decimal totalCashSales, decimal totalCardSales, decimal totalTransferSales, decimal totalOtherSales, decimal totalDiscounts, decimal totalCredits, decimal totalTips);
}

public class CashRegisterService : ICashRegisterService
{
    private readonly ICashRegisterRepository _cashRegisterRepo;
    private readonly IOrderPaymentRepository _orderPaymentRepo;
    private readonly ILogger<CashRegisterService> _logger;

    public CashRegisterService(
        ICashRegisterRepository cashRegisterRepo,
        IOrderPaymentRepository orderPaymentRepo,
        ILogger<CashRegisterService> logger)
    {
        _cashRegisterRepo = cashRegisterRepo;
        _orderPaymentRepo = orderPaymentRepo;
        _logger = logger;
    }

    public async Task<CashRegisterResponse> OpenAsync(long companyId, long branchId, long userId, OpenCashRegisterRequest request)
    {
        if (request.OpeningAmount < 0)
            throw new ValidationException("El monto de apertura no puede ser negativo");

        // Verificar que no tenga caja abierta
        var existing = await _cashRegisterRepo.GetActiveByUserAsync(companyId, branchId, userId);
        if (existing != null)
            throw new BusinessException("Ya tienes una caja abierta. Ciérrala antes de abrir una nueva.");

        var register = new CashRegister
        {
            CompanyId = companyId,
            BranchId = branchId,
            OpenedBy = userId,
            Status = "open",
            OpeningAmount = request.OpeningAmount,
            Notes = request.Notes,
            OpenedAt = DateTime.UtcNow
        };

        var created = await _cashRegisterRepo.OpenAsync(register);
        return MapToResponse(created);
    }

    public async Task<CashRegisterResponse?> GetActiveAsync(long companyId, long branchId, long userId)
    {
        var register = await _cashRegisterRepo.GetActiveByUserAsync(companyId, branchId, userId);
        return register != null ? MapToResponse(register) : null;
    }

    public async Task<CashRegisterResponse> CloseAsync(long id, long companyId, long branchId, long userId, CloseCashRegisterRequest request)
    {
        if (request.ClosingAmount < 0)
            throw new ValidationException("El monto de cierre no puede ser negativo");

        var register = await _cashRegisterRepo.GetByIdAsync(id, companyId, branchId)
            ?? throw new NotFoundException("Caja no encontrada");

        if (register.Status != "open")
            throw new BusinessException("Esta caja ya está cerrada");

        if (register.OpenedBy != userId)
            throw new BusinessException("Solo el usuario que abrió la caja puede cerrarla");

        var closed = await _cashRegisterRepo.CloseAsync(
            id, companyId, branchId, userId, request.ClosingAmount, request.Notes)
            ?? throw new BusinessException("La caja ya fue cerrada o dejo de estar disponible");
        return MapToResponse(closed);
    }

    public async Task<CashMovementResponse> AddMovementAsync(long cashRegisterId, long companyId, long branchId, long userId, CashMovementRequest request)
    {
        var movementType = request.Type?.Trim().ToLowerInvariant();
        if (movementType is not ("in" or "out"))
            throw new ValidationException("El tipo de movimiento debe ser 'in' o 'out'");

        if (request.Amount <= 0)
            throw new ValidationException("El monto debe ser mayor a cero");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("El motivo del movimiento es obligatorio");

        if (request.Reason.Trim().Length > 300)
            throw new ValidationException("El motivo no puede superar 300 caracteres");

        // Verificar que la caja esté abierta
        var register = await _cashRegisterRepo.GetByIdAsync(cashRegisterId, companyId, branchId)
            ?? throw new NotFoundException("Caja no encontrada");

        if (register.Status != "open")
            throw new BusinessException("No se pueden hacer movimientos en una caja cerrada");

        var movement = new CashMovement
        {
            CompanyId = companyId,
            CashRegisterId = cashRegisterId,
            Type = movementType,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            Notes = request.Notes,
            CreatedBy = userId
        };

        var created = await _cashRegisterRepo.AddMovementAsync(movement, branchId)
            ?? throw new BusinessException("La caja ya fue cerrada o dejo de estar disponible");
        return MapToMovementResponse(created);
    }

    public async Task<IEnumerable<CashMovementResponse>> GetMovementsAsync(long cashRegisterId, long companyId, long branchId)
    {
        _ = await _cashRegisterRepo.GetByIdAsync(cashRegisterId, companyId, branchId)
            ?? throw new NotFoundException("Caja no encontrada");
        var movements = await _cashRegisterRepo.GetMovementsAsync(cashRegisterId, companyId, branchId);
        return movements.Select(MapToMovementResponse);
    }

    public async Task<CashRegisterSummaryResponse> GetSummaryAsync(long id, long companyId, long branchId)
    {
        var register = await _cashRegisterRepo.GetByIdAsync(id, companyId, branchId)
            ?? throw new NotFoundException("Caja no encontrada");

        var movements = (await _cashRegisterRepo.GetMovementsAsync(id, companyId, branchId)).ToList();
        var paymentBreakdown = await _orderPaymentRepo.GetSummaryByCashRegisterAsync(id, companyId, branchId);
        var refundTotal = await _cashRegisterRepo.GetCompletedRefundTotalAsync(id, companyId, branchId);
        var cashRefundTotal = movements
            .Where(movement => movement.Type == "out" && movement.Reason == "Devolucion de venta")
            .Sum(movement => movement.Amount);

        return new CashRegisterSummaryResponse(
            Register: MapToResponse(register),
            Movements: movements.Select(MapToMovementResponse).ToList(),
            PaymentBreakdown: paymentBreakdown.Select(p => new PaymentMethodSummaryDto(p.Method, p.TotalAmount, p.TransactionCount)).ToList(),
            RefundTotal: refundTotal,
            ManualCashOut: Math.Max(0, register.CashOut - cashRefundTotal)
        );
    }

    public async Task<(IEnumerable<CashRegisterResponse> Items, int TotalCount)> GetHistoryAsync(
        long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit)
    {
        var totalCount = await _cashRegisterRepo.GetHistoryCountAsync(companyId, branchId, dateFrom, dateTo);
        var items = await _cashRegisterRepo.GetHistoryAsync(companyId, branchId, dateFrom, dateTo, page, limit);
        return (items.Select(MapToResponse), totalCount);
    }

    public async Task UpdateTotalsFromOrderAsync(long cashRegisterId, long companyId, decimal totalSales, decimal totalCashSales, decimal totalCardSales, decimal totalTransferSales, decimal totalOtherSales, decimal totalDiscounts, decimal totalCredits, decimal totalTips)
    {
        var register = await _cashRegisterRepo.GetByIdAsync(cashRegisterId, companyId)
            ?? throw new NotFoundException("Caja no encontrada");

        if (register.Status != "open")
            throw new BusinessException("No se puede facturar en una caja cerrada");

        // UpdateTotalsAsync uses incremental SQL (total_sales = total_sales + @TotalSales)
        await _cashRegisterRepo.UpdateTotalsAsync(
            cashRegisterId, companyId,
            totalSales,
            totalCashSales,
            totalCardSales,
            totalTransferSales,
            totalOtherSales,
            totalDiscounts,
            totalCredits,
            totalTips,
            1);
    }

    private static CashRegisterResponse MapToResponse(CashRegister r) => new(
        r.Id,
        r.BranchId,
        r.OpenedBy,
        r.OpenedByName,
        r.ClosedBy,
        r.ClosedByName,
        r.Status,
        r.OpeningAmount,
        r.ClosingAmount,
        r.ExpectedCash,
        r.Difference,
        r.TotalSales,
        r.TotalCashSales,
        r.TotalCardSales,
        r.TotalTransferSales,
        r.TotalOtherSales,
        r.TotalDiscounts,
        r.TotalCredits,
        r.TotalTips,
        r.CashIn,
        r.CashOut,
        r.OrderCount,
        r.Notes,
        r.OpenedAt,
        r.ClosedAt,
        r.BranchName
    );

    private static CashMovementResponse MapToMovementResponse(CashMovement m) => new(
        m.Id,
        m.Type,
        m.Amount,
        m.Reason,
        m.Notes,
        m.CreatedBy,
        m.CreatedByName,
        m.CreatedAt
    );
}
