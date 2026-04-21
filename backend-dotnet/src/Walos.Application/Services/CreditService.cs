using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface ICreditService
{
    Task<IEnumerable<CreditResponse>> GetCreditsAsync(long companyId, string? status, string? search);
    Task<CreditResponse> GetCreditByIdAsync(long creditId, long companyId);
    Task<CreditResponse> AddPaymentAsync(long creditId, long companyId, long userId, AddCreditPaymentRequest request);
    Task CancelCreditAsync(long creditId, long companyId);
}

public class CreditService : ICreditService
{
    private readonly ICreditRepository _creditRepo;
    private readonly ILogger<CreditService> _logger;

    public CreditService(ICreditRepository creditRepo, ILogger<CreditService> logger)
    {
        _creditRepo = creditRepo;
        _logger = logger;
    }

    public async Task<IEnumerable<CreditResponse>> GetCreditsAsync(long companyId, string? status, string? search)
    {
        var credits = await _creditRepo.GetCreditsAsync(companyId, status, search);
        return credits.Select(MapToResponse);
    }

    public async Task<CreditResponse> GetCreditByIdAsync(long creditId, long companyId)
    {
        var credit = await _creditRepo.GetCreditByIdAsync(creditId, companyId)
            ?? throw new NotFoundException("Credito no encontrado");
        return MapToResponse(credit);
    }

    public async Task<CreditResponse> AddPaymentAsync(long creditId, long companyId, long userId, AddCreditPaymentRequest request)
    {
        if (request.Amount <= 0)
            throw new ValidationException("El monto del abono debe ser mayor a cero");

        var credit = await _creditRepo.GetCreditByIdAsync(creditId, companyId)
            ?? throw new NotFoundException("Credito no encontrado");

        if (credit.Status is "paid" or "cancelled")
            throw new BusinessException("Este credito ya fue saldado o cancelado");

        if (request.Amount > credit.CreditAmount)
            throw new ValidationException($"El abono no puede superar el saldo pendiente de {credit.CreditAmount:N2}");

        await _creditRepo.AddPaymentAsync(new CreditPayment
        {
            CompanyId = companyId,
            CreditId = creditId,
            Amount = request.Amount,
            Notes = request.Notes,
            CreatedBy = userId
        });

        var newAmountPaid   = credit.AmountPaid + request.Amount;
        var newCreditAmount = Math.Round(credit.CreditAmount - request.Amount, 2);
        var newStatus       = newCreditAmount <= 0 ? "paid" : "partial";
        var paidAt          = newStatus == "paid" ? (DateTime?)DateTime.UtcNow : null;

        await _creditRepo.UpdateCreditAfterPaymentAsync(creditId, companyId, newAmountPaid, newCreditAmount, newStatus, paidAt);

        _logger.LogInformation("Abono de {Amount} registrado en credito {CreditId}. Saldo restante: {Remaining}",
            request.Amount, creditId, newCreditAmount);

        return await GetCreditByIdAsync(creditId, companyId);
    }

    public async Task CancelCreditAsync(long creditId, long companyId)
    {
        var credit = await _creditRepo.GetCreditByIdAsync(creditId, companyId)
            ?? throw new NotFoundException("Credito no encontrado");

        if (credit.Status == "paid")
            throw new BusinessException("No se puede cancelar un credito ya pagado");

        await _creditRepo.CancelCreditAsync(creditId, companyId);
    }

    private static CreditResponse MapToResponse(Credit c) => new()
    {
        Id            = c.Id,
        OrderId       = c.OrderId,
        CustomerName  = c.CustomerName,
        OrderNumber   = c.OrderNumber,
        OriginalTotal = c.OriginalTotal,
        AmountPaid    = c.AmountPaid,
        CreditAmount  = c.CreditAmount,
        Status        = c.Status,
        Notes         = c.Notes,
        CreatedAt     = c.CreatedAt,
        PaidAt        = c.PaidAt,
        Payments      = c.Payments?.Select(p => new CreditPaymentResponse
        {
            Id        = p.Id,
            Amount    = p.Amount,
            Notes     = p.Notes,
            CreatedAt = p.CreatedAt
        }).ToList()
    };
}
