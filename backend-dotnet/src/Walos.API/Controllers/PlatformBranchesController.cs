using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Admin;
using Walos.Application.DTOs.Common;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/platform/admin/companies/{companyId:long}/branches")]
[Authorize(Policy = WalosPolicies.PlatformAdmin)]
public sealed class PlatformBranchesController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ITenantContext _tenant;

    public PlatformBranchesController(IAdminService adminService, ITenantContext tenant)
    {
        _adminService = adminService;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetBranches(long companyId)
    {
        var branches = await _adminService.GetBranchesAsync(companyId);
        return Ok(ApiResponse<IReadOnlyList<BranchAdminResponse>>.Ok(branches, count: branches.Count));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch(long companyId, [FromBody] CreateBranchAdminRequest request)
    {
        var branch = await _adminService.CreateBranchAsync(companyId, request, _tenant.UserId);
        return CreatedAtAction(nameof(GetBranches), new { companyId },
            ApiResponse<BranchAdminResponse>.Ok(branch, "Sucursal creada"));
    }

    [HttpPut("{branchId:long}")]
    public async Task<IActionResult> UpdateBranch(
        long companyId,
        long branchId,
        [FromBody] UpdateBranchAdminRequest request)
    {
        var branch = await _adminService.UpdateBranchAsync(companyId, branchId, request, _tenant.UserId);
        return Ok(ApiResponse<BranchAdminResponse>.Ok(branch, "Sucursal actualizada"));
    }
}
