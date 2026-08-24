using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface ICreditService
{
    Task<IEnumerable<CreditResponse>> GetCreditsAsync(long companyId, long branchId, string? status, string? search);
    Task<CreditResponse> GetCreditByIdAsync(long creditId, long companyId, long branchId);
    Task<CreditResponse> AddPaymentAsync(long creditId, long companyId, long branchId, long userId, AddCreditPaymentRequest request);
    Task CancelCreditAsync(long creditId, long companyId, long branchId);
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

    public async Task<IEnumerable<CreditResponse>> GetCreditsAsync(long companyId, long branchId, string? status, string? search)
    {
        var credits = await _creditRepo.GetCreditsAsync(companyId, branchId, status, search);
        return credits.Select(MapToResponse);
    }

    public async Task<CreditResponse> GetCreditByIdAsync(long creditId, long companyId, long branchId)
    {
        var credit = await _creditRepo.GetCreditByIdAsync(creditId, companyId, branchId)
            ?? throw new NotFoundException("Credito no encontrado");
        return MapToResponse(credit);
    }

    public async Task<CreditResponse> AddPaymentAsync(long creditId, long companyId, long branchId, long userId, AddCreditPaymentRequest request)
    {
        if (request.Amount <= 0)
            throw new ValidationException("El monto del abono debe ser mayor a cero");

        var paymentMethod = request.PaymentMethod?.Trim().ToLowerInvariant();
        if (paymentMethod is not ("cash" or "card" or "transfer" or "nequi" or "other"))
            throw new ValidationException("Metodo de pago invalido");

        var credit = await _creditRepo.ProcessPaymentAsync(new CreditPaymentCommand
        {
            CreditId = creditId,
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            Amount = request.Amount,
            PaymentMethod = paymentMethod,
            Notes = request.Notes?.Trim()
        });

        _logger.LogInformation("Abono de {Amount} registrado en credito {CreditId}. Saldo restante: {Remaining}",
            request.Amount, creditId, credit.CreditAmount);

        return MapToResponse(await _creditRepo.GetCreditByIdAsync(creditId, companyId, branchId) ?? credit);
    }

    public async Task CancelCreditAsync(long creditId, long companyId, long branchId)
    {
        var credit = await _creditRepo.GetCreditByIdAsync(creditId, companyId, branchId)
            ?? throw new NotFoundException("Credito no encontrado");

        if (credit.Status == "paid")
            throw new BusinessException("No se puede cancelar un credito ya pagado");

        if (!await _creditRepo.CancelCreditAsync(creditId, companyId, branchId))
            throw new BusinessException("El credito dejo de estar disponible durante la cancelacion");
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
            PaymentMethod = p.PaymentMethod,
            CashRegisterId = p.CashRegisterId,
            Notes     = p.Notes,
            CreatedAt = p.CreatedAt
        }).ToList()
    };
}
