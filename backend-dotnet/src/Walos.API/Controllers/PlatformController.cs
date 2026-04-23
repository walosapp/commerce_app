using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Platform;
using Walos.Domain.Entities.Platform;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Services;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/platform")]
[Authorize]
public class PlatformController : ControllerBase
{
    private readonly IPlatformRepository _platformRepo;
    private readonly ITenantContext _tenant;

    public PlatformController(IPlatformRepository platformRepo, ITenantContext tenant)
    {
        _platformRepo = platformRepo;
        _tenant = tenant;
    }

    [HttpGet("my-plan")]
    public async Task<IActionResult> GetMyPlan()
    {
        var companyId = _tenant.CompanyId;
        var subs = await _platformRepo.GetCompanySubscriptionsAsync(companyId);
        var invoices = (await _platformRepo.GetInvoicesAsync(companyId)).Take(5);
        var aiSettings = await _platformRepo.GetAiSettingsAsync(companyId);

        var plan = new CompanyPlanResponse(
            CompanyId: companyId,
            CompanyName: string.Empty,
            SubscriptionPlan: "basic",
            Services: subs.Select(MapSubscription).ToList(),
            RecentInvoices: invoices.Select(MapInvoice).ToList(),
            AiSettings: MapAiSettings(aiSettings)
        );

        return Ok(ApiResponse<CompanyPlanResponse>.Ok(plan));
    }

    [HttpGet("my-invoices")]
    public async Task<IActionResult> GetMyInvoices()
    {
        var invoices = await _platformRepo.GetInvoicesAsync(_tenant.CompanyId);
        var response = invoices.Select(MapInvoice);
        return Ok(ApiResponse<IEnumerable<BillingInvoiceResponse>>.Ok(response));
    }

    [HttpGet("ai-usage")]
    public async Task<IActionResult> GetAiUsage()
    {
        var settings = await _platformRepo.GetAiSettingsAsync(_tenant.CompanyId);
        return Ok(ApiResponse<CompanyAiSettingsResponse>.Ok(MapAiSettings(settings)));
    }

    [HttpPut("ai-key")]
    public async Task<IActionResult> UpdateAiKey([FromBody] UpdateAiKeyRequest req)
    {
        string? encryptedKey = null;

        if (!req.Managed && !string.IsNullOrWhiteSpace(req.ApiKey))
        {
            encryptedKey = AesEncryptionHelper.Encrypt(req.ApiKey);
        }

        await _platformRepo.UpdateAiKeyAsync(_tenant.CompanyId, encryptedKey, req.Provider, req.Managed);
        return Ok(ApiResponse.Ok("Configuración de IA actualizada"));
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var methods = await _platformRepo.GetPaymentMethodsAsync(_tenant.CompanyId);
        var response = methods.Select(m => new PaymentMethodResponse(
            m.Id, m.Type, m.Provider, m.Last4, m.BankName, m.HolderName, m.IsDefault));
        return Ok(ApiResponse<IEnumerable<PaymentMethodResponse>>.Ok(response));
    }

    [HttpPost("payment-methods")]
    public async Task<IActionResult> AddPaymentMethod([FromBody] RegisterPaymentMethodRequest req)
    {
        var method = new PaymentMethod
        {
            CompanyId = _tenant.CompanyId,
            Type = req.Type,
            ProviderToken = req.ProviderToken,
            Last4 = req.Last4,
            BankName = req.BankName,
            HolderName = req.HolderName,
            IsDefault = req.IsDefault
        };

        var created = await _platformRepo.CreatePaymentMethodAsync(method);
        var response = new PaymentMethodResponse(
            created.Id, created.Type, created.Provider, created.Last4,
            created.BankName, created.HolderName, created.IsDefault);

        return Ok(ApiResponse<PaymentMethodResponse>.Ok(response, "Método de pago registrado"));
    }

    [HttpPatch("payment-methods/{id:long}/default")]
    public async Task<IActionResult> SetDefault(long id)
    {
        await _platformRepo.SetDefaultPaymentMethodAsync(id, _tenant.CompanyId);
        return Ok(ApiResponse.Ok("Método de pago por defecto actualizado"));
    }

    [HttpDelete("payment-methods/{id:long}")]
    public async Task<IActionResult> DeletePaymentMethod(long id)
    {
        await _platformRepo.DeletePaymentMethodAsync(id, _tenant.CompanyId);
        return Ok(ApiResponse.Ok("Método de pago eliminado"));
    }

    private static CompanySubscriptionResponse MapSubscription(CompanySubscription s) =>
        new(s.Id, s.CompanyId, s.ServiceCode, s.ServiceName ?? string.Empty,
            s.IsActive, s.CustomPrice, s.BasePrice ?? 0, s.EffectivePrice,
            s.BillingFrequency, s.NextBillingDate, s.StartedAt, s.CancelledAt, s.Notes);

    private static BillingInvoiceResponse MapInvoice(BillingInvoice i) =>
        new(i.Id, i.CompanyId, i.CompanyName, i.InvoiceNumber, i.PeriodStart, i.PeriodEnd,
            i.Subtotal, i.TaxRate, i.TaxAmount, i.Total, i.Status,
            i.SentAt, i.PaidAt, i.DueDate, i.PaymentMethod, i.PaymentRef, i.Notes, i.CreatedAt,
            i.Items.Select(item => new BillingInvoiceItemResponse(
                item.Id, item.ServiceCode, item.Description, item.Quantity, item.UnitPrice, item.Subtotal)).ToList());

    private static CompanyAiSettingsResponse MapAiSettings(CompanyAiSettings a) =>
        new(a.CompanyId, a.AiKeyManaged, a.AiProvider, a.HasCustomKey,
            a.AiTokensUsed, a.AiTokensResetAt, a.AiEstimatedCost);
}
