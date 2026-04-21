using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Admin;
using Walos.Application.DTOs.Common;
using Walos.Application.Services;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "dev")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await _adminService.GetTenantsAsync();
        var list = tenants.ToList();
        return Ok(ApiResponse<IEnumerable<TenantResponse>>.Ok(list, count: list.Count));
    }

    [HttpGet("tenants/{id:long}")]
    public async Task<IActionResult> GetTenant(long id)
    {
        var tenant = await _adminService.GetTenantByIdAsync(id);
        if (tenant is null)
            return NotFound(ApiResponse.Fail("Comercio no encontrado"));

        return Ok(ApiResponse<TenantResponse>.Ok(tenant));
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var result = await _adminService.CreateTenantAsync(request);
        return Created($"api/v1/admin/tenants/{result.Company.Id}",
            ApiResponse<CreateTenantResult>.Ok(result, "Comercio creado exitosamente"));
    }

    [HttpPut("tenants/{id:long}")]
    public async Task<IActionResult> UpdateTenant(long id, [FromBody] UpdateTenantRequest request)
    {
        var updated = await _adminService.UpdateTenantAsync(id, request);
        if (updated is null)
            return NotFound(ApiResponse.Fail("Comercio no encontrado"));

        return Ok(ApiResponse<TenantResponse>.Ok(updated, "Comercio actualizado"));
    }

    [HttpPatch("tenants/{id:long}/status")]
    public async Task<IActionResult> SetTenantStatus(long id, [FromBody] SetTenantStatusRequest request)
    {
        var updated = await _adminService.SetTenantActiveAsync(id, request.IsActive);
        if (!updated)
            return NotFound(ApiResponse.Fail("Comercio no encontrado"));

        return Ok(ApiResponse.Ok(request.IsActive ? "Comercio activado" : "Comercio desactivado"));
    }

    public record SetTenantStatusRequest(bool IsActive);
}
