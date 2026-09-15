using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
[RequireAnyFeature(WalosFeatures.Suppliers, WalosFeatures.Purchases)]
public class SuppliersController : ControllerBase
{
    private readonly ISuppliersService _service;
    private readonly ITenantContext _tenant;

    public SuppliersController(ISuppliersService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Policy = WalosPolicies.SuppliersRead)]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync(_tenant.CompanyId, _tenant.BranchId);
        var list = items.ToList();
        return Ok(ApiResponse<IEnumerable<Supplier>>.Ok(list, count: list.Count));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = WalosPolicies.SuppliersRead)]
    public async Task<IActionResult> GetById(long id)
    {
        var supplier = await _service.GetByIdAsync(id, _tenant.CompanyId, _tenant.BranchId);
        if (supplier is null)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));
        return Ok(ApiResponse<Supplier>.Ok(supplier));
    }

    [HttpPost]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    [RequireFeature(WalosFeatures.Suppliers)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
    {
        if (_tenant.BranchId.HasValue && request.BranchId.HasValue &&
            request.BranchId.Value != _tenant.BranchId.Value)
        {
            throw new ValidationException("La sucursal del proveedor no coincide con la sucursal autenticada");
        }

        var branchId = _tenant.BranchId ?? request.BranchId;
        var created = await _service.CreateAsync(_tenant.CompanyId, branchId, _tenant.UserId, request);
        return Created($"api/v1/suppliers/{created.Id}", ApiResponse<Supplier>.Ok(created, "Proveedor creado"));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    [RequireFeature(WalosFeatures.Suppliers)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplierRequest request)
    {
        var updated = await _service.UpdateAsync(_tenant.CompanyId, _tenant.BranchId, id, request);
        if (updated is null)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));
        return Ok(ApiResponse<Supplier>.Ok(updated, "Proveedor actualizado"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    [RequireFeature(WalosFeatures.Suppliers)]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _service.DeleteAsync(id, _tenant.CompanyId, _tenant.BranchId);
        if (!deleted)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));
        return Ok(ApiResponse.Ok("Proveedor eliminado"));
    }

    [HttpPost("{id:long}/products")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    [RequireFeature(WalosFeatures.Suppliers)]
    public async Task<IActionResult> AddProduct(long id, [FromBody] AddSupplierProductRequest request)
    {
        var result = await _service.AddProductAsync(_tenant.CompanyId, _tenant.BranchId, id, request);
        return Ok(ApiResponse<SupplierProduct>.Ok(result, "Producto asociado"));
    }

    [HttpDelete("{id:long}/products/{productId:long}")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    [RequireFeature(WalosFeatures.Suppliers)]
    public async Task<IActionResult> RemoveProduct(long id, long productId)
    {
        var removed = await _service.RemoveProductAsync(_tenant.CompanyId, _tenant.BranchId, id, productId);
        if (!removed)
            return NotFound(ApiResponse.Fail("Asociacion no encontrada"));
        return Ok(ApiResponse.Ok("Producto desasociado"));
    }

    [HttpGet("{id:long}/suggested-order")]
    [Authorize(Policy = WalosPolicies.SuppliersRead)]
    public async Task<IActionResult> GetSuggestedOrder(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var response = await _service.GetSuggestedOrderAsync(id, _tenant.CompanyId, _tenant.BranchId.Value);
        if (response is null)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));

        return Ok(ApiResponse<SuggestedOrderResponse>.Ok(response));
    }
}
