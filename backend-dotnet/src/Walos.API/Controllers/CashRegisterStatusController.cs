using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

// Separate from CashRegisterController: operational readers must not receive its financial DTO.
[ApiController]
[Route("api/v1/sales/cash-register/status")]
[Authorize(Policy = WalosPolicies.SalesTableOperator)]
[RequireFeature(WalosFeatures.Cash)]
[RequireAnyFeature(WalosFeatures.Restaurant, WalosFeatures.Pos)]
public class CashRegisterStatusController : ControllerBase
{
    private readonly ICashRegisterService _service;
    private readonly ITenantContext _tenant;

    public CashRegisterStatusController(ICashRegisterService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var status = await _service.GetStatusAsync(_tenant.CompanyId, _tenant.BranchId.Value);
        return Ok(ApiResponse<CashRegisterStatusResponse>.Ok(status));
    }
}
