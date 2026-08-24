using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Ai;
using Walos.Application.DTOs.Common;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize(Policy = WalosPolicies.InventoryWrite)]
public class AiController : ControllerBase
{
    private readonly OrchestratorService _orchestrator;
    private readonly ITenantContext _tenant;
    private readonly ICompanyService _companyService;

    public AiController(OrchestratorService orchestrator, ITenantContext tenant, ICompanyService companyService)
    {
        _orchestrator = orchestrator;
        _tenant = tenant;
        _companyService = companyService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(ApiResponse.Fail("El mensaje no puede estar vacío"));

        var settings = await _companyService.GetSettingsAsync(_tenant.CompanyId);
        var companyName = settings.DisplayName ?? settings.Name ?? "tu negocio";

        var response = await _orchestrator.ChatAsync(
            _tenant.CompanyId,
            _tenant.UserId,
            _tenant.BranchId ?? 0,
            companyName,
            request.Message,
            request.SessionId);

        return Ok(ApiResponse<AiChatResponse>.Ok(response));
    }
}
