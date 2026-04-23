using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Company;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/company")]
[Authorize]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ITenantContext _tenant;

    public CompanyController(ICompanyService companyService, ITenantContext tenant)
    {
        _companyService = companyService;
        _tenant = tenant;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _companyService.GetSettingsAsync(_tenant.CompanyId);
        return Ok(ApiResponse<CompanySettings>.Ok(settings));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateCompanySettingsRequest request)
    {
        var updated = await _companyService.UpdateSettingsAsync(_tenant.CompanyId, _tenant.UserId, request);
        return Ok(ApiResponse<CompanySettings>.Ok(updated, "Configuracion actualizada exitosamente"));
    }

    [HttpGet("settings/operations")]
    public async Task<IActionResult> GetOperationsSettings()
    {
        var settings = await _companyService.GetOperationsSettingsAsync(_tenant.CompanyId);
        return Ok(ApiResponse<CompanyOperationsSettings>.Ok(settings));
    }

    [HttpPut("settings/operations")]
    public async Task<IActionResult> UpdateOperationsSettings([FromBody] UpdateCompanyOperationsSettingsRequest request)
    {
        var updated = await _companyService.UpdateOperationsSettingsAsync(_tenant.CompanyId, request);
        return Ok(ApiResponse<CompanyOperationsSettings>.Ok(updated, "Reglas operativas actualizadas exitosamente"));
    }

    [HttpPost("settings/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No se proporciono archivo"));

        var logoUrl = await _companyService.UploadLogoAsync(
            _tenant.CompanyId, _tenant.UserId, file.OpenReadStream(), file.FileName, file.ContentType);

        return Ok(ApiResponse<object>.Ok(new { logoUrl }, "Logo actualizado exitosamente"));
    }

    [HttpDelete("settings/logo")]
    public async Task<IActionResult> RemoveLogo()
    {
        await _companyService.RemoveLogoAsync(_tenant.CompanyId, _tenant.UserId);
        return Ok(ApiResponse.Ok("Logo eliminado exitosamente"));
    }
}
