using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales/credits")]
[Authorize]
public class CreditController : ControllerBase
{
    private readonly ICreditService _creditService;
    private readonly ITenantContext _tenant;

    public CreditController(ICreditService creditService, ITenantContext tenant)
    {
        _creditService = creditService;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetCredits([FromQuery] string? status, [FromQuery] string? search)
    {
        var credits = (await _creditService.GetCreditsAsync(_tenant.CompanyId, status, search)).ToList();
        return Ok(ApiResponse<List<CreditResponse>>.Ok(credits, count: credits.Count));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetCredit(long id)
    {
        var credit = await _creditService.GetCreditByIdAsync(id, _tenant.CompanyId);
        return Ok(ApiResponse<CreditResponse>.Ok(credit));
    }

    [HttpPost("{id:long}/pay")]
    public async Task<IActionResult> AddPayment(long id, [FromBody] AddCreditPaymentRequest request)
    {
        var credit = await _creditService.AddPaymentAsync(id, _tenant.CompanyId, _tenant.UserId, request);
        return Ok(ApiResponse<CreditResponse>.Ok(credit, "Abono registrado exitosamente"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> CancelCredit(long id)
    {
        await _creditService.CancelCreditAsync(id, _tenant.CompanyId);
        return Ok(ApiResponse.Ok("Credito cancelado"));
    }
}
