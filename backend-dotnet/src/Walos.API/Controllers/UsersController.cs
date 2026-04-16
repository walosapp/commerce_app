using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Users;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUsersRepository _repo;
    private readonly ITenantContext _tenant;

    public UsersController(IUsersRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _repo.GetAllAsync(_tenant.CompanyId);
        var list = users.ToList();
        return Ok(ApiResponse<IEnumerable<User>>.Ok(list, count: list.Count));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _repo.GetRolesAsync(_tenant.CompanyId, excludeDev: true);
        return Ok(ApiResponse<IEnumerable<RoleOption>>.Ok(roles));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var user = await _repo.GetByIdAsync(id, _tenant.CompanyId);
        if (user is null)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse<User>.Ok(user));
    }

    [HttpPost]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(ApiResponse.Fail("Nombre y apellido son requeridos"));

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse.Fail("El email es requerido"));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(ApiResponse.Fail("La contraseña debe tener al menos 6 caracteres"));

        if (await _repo.EmailExistsAsync(request.Email))
            return Conflict(ApiResponse.Fail("Ya existe un usuario con ese email"));

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User
        {
            CompanyId = _tenant.CompanyId,
            BranchId  = request.BranchId,
            RoleId    = request.RoleId,
            FirstName = request.FirstName,
            LastName  = request.LastName,
            Email     = request.Email,
            Phone     = request.Phone,
        };

        var created = await _repo.CreateAsync(user, passwordHash);
        return Created($"api/v1/users/{created.Id}", ApiResponse<User>.Ok(created, "Usuario creado exitosamente"));
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateUserRequest request)
    {
        var user = new User
        {
            Id        = id,
            CompanyId = _tenant.CompanyId,
            FirstName = request.FirstName,
            LastName  = request.LastName,
            Phone     = request.Phone,
            RoleId    = request.RoleId,
            BranchId  = request.BranchId,
        };

        var updated = await _repo.UpdateAsync(user);
        if (updated is null)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse<User>.Ok(updated, "Usuario actualizado"));
    }

    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] bool isActive)
    {
        if (id == _tenant.UserId)
            return BadRequest(ApiResponse.Fail("No puedes desactivar tu propio usuario"));

        var ok = await _repo.SetActiveAsync(id, _tenant.CompanyId, isActive);
        if (!ok)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse.Ok(isActive ? "Usuario activado" : "Usuario desactivado"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "dev,admin")]
    public async Task<IActionResult> Delete(long id)
    {
        if (id == _tenant.UserId)
            return BadRequest(ApiResponse.Fail("No puedes eliminar tu propio usuario"));

        var ok = await _repo.SoftDeleteAsync(id, _tenant.CompanyId);
        if (!ok)
            return NotFound(ApiResponse.Fail("Usuario no encontrado"));
        return Ok(ApiResponse.Ok("Usuario eliminado"));
    }
}
