using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Users;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = WalosPolicies.Users)]
public class UsersController : ControllerBase
{
    private readonly IUsersService _service;
    private readonly ITenantContext _tenant;

    public UsersController(IUsersService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _service.GetAllAsync(_tenant.CompanyId);
        var list = users.ToList();
        return Ok(ApiResponse<IEnumerable<User>>.Ok(list, count: list.Count));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _service.GetRolesAsync(_tenant.CompanyId);
        return Ok(ApiResponse<IEnumerable<RoleOption>>.Ok(roles));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var user = await _service.GetByIdAsync(id, _tenant.CompanyId);
        if (user is null)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse<User>.Ok(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var created = await _service.CreateAsync(
            _tenant.CompanyId, _tenant.UserId, _tenant.Role, request);
        return Created($"api/v1/users/{created.Id}", ApiResponse<User>.Ok(created, "Usuario creado exitosamente"));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateUserRequest request)
    {
        var updated = await _service.UpdateAsync(
            _tenant.CompanyId, _tenant.UserId, _tenant.Role, id, request);
        if (updated is null)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse<User>.Ok(updated, "Usuario actualizado"));
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] bool isActive)
    {
        var ok = await _service.SetStatusAsync(
            _tenant.CompanyId, _tenant.UserId, _tenant.Role, id, isActive);
        if (!ok)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse.Ok(isActive ? "Usuario activado" : "Usuario desactivado"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _service.DeleteAsync(_tenant.CompanyId, _tenant.UserId, _tenant.Role, id);
        if (!ok)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse.Ok("Usuario eliminado"));
    }

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest request)
    {
        var updated = await _service.ResetPasswordAsync(
            _tenant.CompanyId,
            _tenant.UserId,
            _tenant.Role,
            id,
            request.NewPassword);
        if (!updated)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));

        return Ok(ApiResponse.Ok("Contraseña actualizada"));
    }

    public sealed record ResetPasswordRequest(string NewPassword);
}
