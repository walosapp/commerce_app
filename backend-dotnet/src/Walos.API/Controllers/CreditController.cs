using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales/credits")]
[Authorize(Policy = WalosPolicies.CashOperator)]
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
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var credits = (await _creditService.GetCreditsAsync(
            _tenant.CompanyId, _tenant.BranchId.Value, status, search)).ToList();
        return Ok(ApiResponse<List<CreditResponse>>.Ok(credits, count: credits.Count));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetCredit(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var credit = await _creditService.GetCreditByIdAsync(id, _tenant.CompanyId, _tenant.BranchId.Value);
        return Ok(ApiResponse<CreditResponse>.Ok(credit));
    }

    [HttpPost("{id:long}/pay")]
    public async Task<IActionResult> AddPayment(long id, [FromBody] AddCreditPaymentRequest request)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var credit = await _creditService.AddPaymentAsync(
            id, _tenant.CompanyId, _tenant.BranchId.Value, _tenant.UserId, request);
        return Ok(ApiResponse<CreditResponse>.Ok(credit, "Abono registrado exitosamente"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> CancelCredit(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        await _creditService.CancelCreditAsync(id, _tenant.CompanyId, _tenant.BranchId.Value);
        return Ok(ApiResponse.Ok("Credito cancelado"));
    }
}
