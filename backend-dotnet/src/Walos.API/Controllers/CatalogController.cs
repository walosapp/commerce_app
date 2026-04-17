using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly ICatalogRepository _repo;
    private readonly ITenantContext _tenant;

    public CatalogController(ICatalogRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    // ─── CATEGORIES ────────────────────────────────────────────────────────────

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var items = (await _repo.GetCategoriesAsync(_tenant.CompanyId)).ToList();
        return Ok(ApiResponse<IEnumerable<CategoryResponse>>.Ok(items, count: items.Count));
    }

    [HttpPost("categories")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre es requerido"));

        var result = await _repo.CreateCategoryAsync(_tenant.CompanyId, request);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Categoria creada"));
    }

    [HttpPut("categories/{id:long}")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] SaveCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre es requerido"));

        var result = await _repo.UpdateCategoryAsync(id, _tenant.CompanyId, request);
        if (result is null) return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Categoria actualizada"));
    }

    [HttpPatch("categories/{id:long}/status")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> SetCategoryStatus(long id, [FromBody] SetStatusRequest request)
    {
        var ok = await _repo.SetCategoryActiveAsync(id, _tenant.CompanyId, request.IsActive);
        if (!ok) return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        return Ok(ApiResponse.Ok(request.IsActive ? "Categoria activada" : "Categoria desactivada"));
    }

    [HttpDelete("categories/{id:long}")]
    [Authorize(Roles = "dev,admin")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        try
        {
            var ok = await _repo.DeleteCategoryAsync(id, _tenant.CompanyId);
            if (!ok) return NotFound(ApiResponse.Fail("Categoria no encontrada"));
            return Ok(ApiResponse.Ok("Categoria eliminada"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ─── UNITS ─────────────────────────────────────────────────────────────────

    [HttpGet("units")]
    public async Task<IActionResult> GetUnits()
    {
        var items = (await _repo.GetUnitsAsync(_tenant.CompanyId)).ToList();
        return Ok(ApiResponse<IEnumerable<UnitResponse>>.Ok(items, count: items.Count));
    }

    [HttpPost("units")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> CreateUnit([FromBody] SaveUnitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Abbreviation))
            return BadRequest(ApiResponse.Fail("Nombre y abreviatura son requeridos"));
        try
        {
            var result = await _repo.CreateUnitAsync(_tenant.CompanyId, request);
            return Ok(ApiResponse<UnitResponse>.Ok(result, "Unidad creada"));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("units/{id:long}")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> UpdateUnit(long id, [FromBody] SaveUnitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre es requerido"));

        var result = await _repo.UpdateUnitAsync(id, _tenant.CompanyId, request);
        if (result is null) return NotFound(ApiResponse.Fail("Unidad no encontrada"));
        return Ok(ApiResponse<UnitResponse>.Ok(result, "Unidad actualizada"));
    }

    [HttpPatch("units/{id:long}/status")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> SetUnitStatus(long id, [FromBody] SetStatusRequest request)
    {
        var ok = await _repo.SetUnitActiveAsync(id, _tenant.CompanyId, request.IsActive);
        if (!ok) return NotFound(ApiResponse.Fail("Unidad no encontrada"));
        return Ok(ApiResponse.Ok(request.IsActive ? "Unidad activada" : "Unidad desactivada"));
    }

    [HttpDelete("units/{id:long}")]
    [Authorize(Roles = "dev,admin")]
    public async Task<IActionResult> DeleteUnit(long id)
    {
        try
        {
            var ok = await _repo.DeleteUnitAsync(id, _tenant.CompanyId);
            if (!ok) return NotFound(ApiResponse.Fail("Unidad no encontrada"));
            return Ok(ApiResponse.Ok("Unidad eliminada"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}

public class SetStatusRequest
{
    public bool IsActive { get; set; }
}
