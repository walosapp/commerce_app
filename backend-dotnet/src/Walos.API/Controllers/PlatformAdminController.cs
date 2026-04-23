using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Platform;
using Walos.Domain.Entities.Platform;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/platform/admin")]
[Authorize]
public class PlatformAdminController : ControllerBase
{
    private readonly IPlatformRepository _platformRepo;

    public PlatformAdminController(IPlatformRepository platformRepo)
    {
        _platformRepo = platformRepo;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetServiceCatalog()
    {
        var catalog = await _platformRepo.GetServiceCatalogAsync();
        var response = catalog.Select(s => new ServiceCatalogResponse(
            s.Id, s.Code, s.Name, s.Description, s.BasePrice,
            s.BillingUnit, s.IsActive, s.DisplayOrder));
        return Ok(ApiResponse<IEnumerable<ServiceCatalogResponse>>.Ok(response));
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var companies = await _platformRepo.GetAllCompaniesForBillingAsync();
        var items = new List<AdminCompanyListItem>();

        foreach (var (companyId, companyName) in companies)
        {
            var subs = (await _platformRepo.GetCompanySubscriptionsAsync(companyId)).ToList();
            var invoices = (await _platformRepo.GetInvoicesAsync(companyId)).ToList();
            var pending = invoices.FirstOrDefault(i => i.Status is "sent" or "overdue");

            items.Add(new AdminCompanyListItem(
                Id: companyId,
                Name: companyName,
                SubscriptionPlan: "basic",
                IsActive: true,
                ActiveServices: subs.Count(s => s.IsActive),
                NextBillingDate: subs.Where(s => s.IsActive && s.NextBillingDate.HasValue)
                                     .Min(s => s.NextBillingDate),
                PendingInvoiceStatus: pending?.Status
            ));
        }

        return Ok(ApiResponse<List<AdminCompanyListItem>>.Ok(items));
    }

    [HttpGet("companies/{companyId:long}/plan")]
    public async Task<IActionResult> GetCompanyPlan(long companyId)
    {
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

    [HttpPost("companies/{companyId:long}/services")]
    public async Task<IActionResult> AssignService(long companyId, [FromBody] AssignServiceRequest req)
    {
        var subscription = new CompanySubscription
        {
            CompanyId = companyId,
            ServiceCode = req.ServiceCode,
            IsActive = req.IsActive,
            CustomPrice = req.CustomPrice,
            BillingFrequency = req.BillingFrequency,
            NextBillingDate = req.NextBillingDate,
            Notes = req.Notes
        };

        await _platformRepo.UpsertSubscriptionAsync(subscription);
        return Ok(ApiResponse.Ok("Servicio asignado"));
    }

    [HttpPatch("companies/{companyId:long}/services/{serviceCode}")]
    public async Task<IActionResult> UpdateService(long companyId, string serviceCode, [FromBody] AssignServiceRequest req)
    {
        var subscription = new CompanySubscription
        {
            CompanyId = companyId,
            ServiceCode = serviceCode,
            IsActive = req.IsActive,
            CustomPrice = req.CustomPrice,
            BillingFrequency = req.BillingFrequency,
            NextBillingDate = req.NextBillingDate,
            Notes = req.Notes
        };

        await _platformRepo.UpsertSubscriptionAsync(subscription);
        return Ok(ApiResponse.Ok("Servicio actualizado"));
    }

    [HttpPost("companies/{companyId:long}/invoices")]
    public async Task<IActionResult> GenerateInvoice(long companyId, [FromBody] GenerateInvoiceRequest req)
    {
        var subtotal = req.Items.Sum(i => i.Quantity * i.UnitPrice);
        var taxRate = 19m;
        var taxAmount = Math.Round(subtotal * taxRate / 100, 2);
        var total = subtotal + taxAmount;

        var invoice = new BillingInvoice
        {
            CompanyId = companyId,
            InvoiceNumber = $"WAL-{DateTime.UtcNow:yyyyMMdd}-{companyId:D4}",
            PeriodStart = req.PeriodStart,
            PeriodEnd = req.PeriodEnd,
            Subtotal = subtotal,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            Total = total,
            Status = "draft",
            DueDate = req.DueDate,
            Notes = req.Notes
        };

        var items = req.Items.Select(i => new BillingInvoiceItem
        {
            ServiceCode = i.ServiceCode,
            Description = i.Description,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.Quantity * i.UnitPrice
        });

        var created = await _platformRepo.CreateInvoiceAsync(invoice, items);
        return Ok(ApiResponse<BillingInvoiceResponse>.Ok(MapInvoice(created), "Factura generada"));
    }

    [HttpPatch("invoices/{id:long}/status")]
    public async Task<IActionResult> UpdateInvoiceStatus(long id, [FromBody] UpdateInvoiceStatusRequest req)
    {
        await _platformRepo.UpdateInvoiceStatusAsync(id, req.Status, req.PaymentRef);
        return Ok(ApiResponse.Ok("Estado de factura actualizado"));
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
