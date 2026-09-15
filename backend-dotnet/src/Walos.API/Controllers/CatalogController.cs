using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/catalog")]
[Authorize]
[RequireAnyFeature(WalosFeatures.Inventory, WalosFeatures.Restaurant, WalosFeatures.Pos,
    WalosFeatures.Purchases, WalosFeatures.Suppliers)]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _service;
    private readonly ITenantContext _tenant;

    public CatalogController(ICatalogService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var items = (await _service.GetCategoriesAsync(_tenant.CompanyId)).ToList();
        return Ok(ApiResponse<IEnumerable<CategoryResponse>>.Ok(items, count: items.Count));
    }

    [HttpPost("categories")]
    [Authorize(Policy = WalosPolicies.CatalogWrite)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> CreateCategory([FromBody] SaveCategoryRequest request)
    {
        var result = await _service.CreateCategoryAsync(_tenant.CompanyId, request);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Categoria creada"));
    }

    [HttpPut("categories/{id:long}")]
    [Authorize(Policy = WalosPolicies.CatalogWrite)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] SaveCategoryRequest request)
    {
        var result = await _service.UpdateCategoryAsync(id, _tenant.CompanyId, request);
        if (result is null) return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Categoria actualizada"));
    }

    [HttpPatch("categories/{id:long}/status")]
    [Authorize(Policy = WalosPolicies.CatalogWrite)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> SetCategoryStatus(long id, [FromBody] SetStatusRequest request)
    {
        var ok = await _service.SetCategoryStatusAsync(id, _tenant.CompanyId, request.IsActive);
        if (!ok) return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        return Ok(ApiResponse.Ok(request.IsActive ? "Categoria activada" : "Categoria desactivada"));
    }

    [HttpDelete("categories/{id:long}")]
    [Authorize(Policy = WalosPolicies.CatalogDelete)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        try
        {
            var ok = await _service.DeleteCategoryAsync(id, _tenant.CompanyId);
            if (!ok) return NotFound(ApiResponse.Fail("Categoria no encontrada"));
            return Ok(ApiResponse.Ok("Categoria eliminada"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("units")]
    public async Task<IActionResult> GetUnits()
    {
        var items = (await _service.GetUnitsAsync(_tenant.CompanyId)).ToList();
        return Ok(ApiResponse<IEnumerable<UnitResponse>>.Ok(items, count: items.Count));
    }

    [HttpPost("units")]
    [Authorize(Policy = WalosPolicies.CatalogWrite)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> CreateUnit([FromBody] SaveUnitRequest request)
    {
        try
        {
            var result = await _service.CreateUnitAsync(_tenant.CompanyId, request);
            return Ok(ApiResponse<UnitResponse>.Ok(result, "Unidad creada"));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("units/{id:long}")]
    [Authorize(Policy = WalosPolicies.CatalogWrite)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> UpdateUnit(long id, [FromBody] SaveUnitRequest request)
    {
        var result = await _service.UpdateUnitAsync(id, _tenant.CompanyId, request);
        if (result is null) return NotFound(ApiResponse.Fail("Unidad no encontrada"));
        return Ok(ApiResponse<UnitResponse>.Ok(result, "Unidad actualizada"));
    }

    [HttpPatch("units/{id:long}/status")]
    [Authorize(Policy = WalosPolicies.CatalogWrite)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> SetUnitStatus(long id, [FromBody] SetStatusRequest request)
    {
        var ok = await _service.SetUnitStatusAsync(id, _tenant.CompanyId, request.IsActive);
        if (!ok) return NotFound(ApiResponse.Fail("Unidad no encontrada"));
        return Ok(ApiResponse.Ok(request.IsActive ? "Unidad activada" : "Unidad desactivada"));
    }

    [HttpDelete("units/{id:long}")]
    [Authorize(Policy = WalosPolicies.CatalogDelete)]
    [RequireFeature(WalosFeatures.Inventory)]
    public async Task<IActionResult> DeleteUnit(long id)
    {
        try
        {
            var ok = await _service.DeleteUnitAsync(id, _tenant.CompanyId);
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
