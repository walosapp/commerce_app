using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Admin;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Users;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = WalosPolicies.PlatformAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IUsersRepository _usersRepo;
    private readonly ITenantContext _tenant;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        IUsersRepository usersRepo,
        ITenantContext tenant,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _usersRepo = usersRepo;
        _tenant = tenant;
        _logger = logger;
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

    [HttpPost("tenants/{id:long}/reset-password")]
    public async Task<IActionResult> ResetAdminPassword(long id, [FromBody] ResetPasswordRequest request)
    {
        await _adminService.ResetTenantAdminPasswordAsync(id, _tenant.UserId, request.NewPassword);
        return Ok(ApiResponse.Ok("Contraseña del administrador actualizada"));
    }

    [HttpPatch("tenants/{id:long}/status")]
    public async Task<IActionResult> SetTenantStatus(long id, [FromBody] SetTenantStatusRequest request)
    {
        var updated = await _adminService.SetTenantActiveAsync(id, request.IsActive);
        if (!updated)
            return NotFound(ApiResponse.Fail("Comercio no encontrado"));

        return Ok(ApiResponse.Ok(request.IsActive ? "Comercio activado" : "Comercio desactivado"));
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] long? companyId)
    {
        var users = (await _usersRepo.GetAllGlobalAsync(companyId)).ToList();
        return Ok(ApiResponse<List<User>>.Ok(users, count: users.Count));
    }

    [HttpGet("users/roles")]
    public async Task<IActionResult> GetRolesForCompany([FromQuery] long companyId)
    {
        var roles = await _usersRepo.GetRolesAsync(companyId, excludeDev: true);
        return Ok(ApiResponse<IEnumerable<RoleOption>>.Ok(roles));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUserInTenant([FromBody] CreateUserInTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(ApiResponse.Fail("Nombre y apellido son requeridos"));
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse.Fail("El email es requerido"));
        PasswordPolicy.Validate(request.Password);
        if (await _usersRepo.EmailExistsAsync(request.Email))
            return Conflict(ApiResponse.Fail("Ya existe un usuario con ese email"));

        var role = await _usersRepo.GetRoleForAssignmentAsync(request.RoleId, request.CompanyId);
        if (role is null || string.Equals(role.Code, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("El rol no pertenece al comercio, esta inactivo o no puede asignarse"));

        if (request.BranchId.HasValue
            && !await _usersRepo.IsActiveBranchInCompanyAsync(request.BranchId.Value, request.CompanyId))
            return BadRequest(ApiResponse.Fail("La sucursal no pertenece al comercio o esta inactiva"));

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User
        {
            CompanyId = request.CompanyId,
            BranchId  = request.BranchId,
            RoleId    = request.RoleId,
            FirstName = request.FirstName.Trim(),
            LastName  = request.LastName.Trim(),
            Email     = request.Email.Trim(),
            Phone     = request.Phone,
        };
        var created = await _usersRepo.CreateAsync(user, hash);
        return Created($"api/v1/admin/users/{created.Id}", ApiResponse<User>.Ok(created, "Usuario creado"));
    }

    [HttpPatch("users/{id:long}/status")]
    public async Task<IActionResult> SetUserStatus(long id, [FromQuery] long companyId, [FromBody] bool isActive)
    {
        var target = await _usersRepo.GetByIdAsync(id, companyId);
        if (target is null)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        if (string.Equals(target.RoleCode, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("La cuenta tecnica dev esta protegida", "protected_technical_account"));

        var ok = await _usersRepo.SetActiveAsync(id, companyId, isActive);
        if (!ok) return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse.Ok(isActive ? "Usuario activado" : "Usuario desactivado"));
    }

    [HttpPost("users/{id:long}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(long id, [FromQuery] long companyId, [FromBody] ResetPasswordRequest request)
    {
        if (id == _tenant.UserId)
            return BadRequest(ApiResponse.Fail("Use change-password to update your own password"));

        var target = await _usersRepo.GetByIdAsync(id, companyId);
        if (target is null)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        if (string.Equals(target.RoleCode, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("La cuenta tecnica dev esta protegida", "protected_technical_account"));

        PasswordPolicy.Validate(request.NewPassword);
        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        var ok = await _usersRepo.ResetPasswordByPlatformActorAsync(
            _tenant.UserId,
            id,
            companyId,
            hash);
        if (!ok) return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        _logger.LogWarning(
            "PasswordReset ActorUserId {ActorUserId} TargetUserId {TargetUserId} CompanyId {CompanyId}; target sessions invalidated",
            _tenant.UserId,
            id,
            companyId);
        return Ok(ApiResponse.Ok("Contraseña actualizada"));
    }

    public record SetTenantStatusRequest(bool IsActive);
    public record ResetPasswordRequest(string NewPassword);
    public record CreateUserInTenantRequest(
        long CompanyId, long RoleId, long? BranchId,
        string FirstName, string LastName, string Email,
        string Password, string? Phone);
}
