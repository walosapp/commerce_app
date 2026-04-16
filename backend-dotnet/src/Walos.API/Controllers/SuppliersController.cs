using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISuppliersRepository _repo;
    private readonly ITenantContext _tenant;

    public SuppliersController(ISuppliersRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _repo.GetAllAsync(_tenant.CompanyId, _tenant.BranchId);
        var list = items.ToList();
        return Ok(ApiResponse<IEnumerable<Supplier>>.Ok(list, count: list.Count));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var supplier = await _repo.GetByIdAsync(id, _tenant.CompanyId);
        if (supplier is null)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));
        return Ok(ApiResponse<Supplier>.Ok(supplier));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre es requerido"));

        var supplier = new Supplier
        {
            CompanyId   = _tenant.CompanyId,
            BranchId    = _tenant.BranchId,
            Name        = request.Name,
            ContactName = request.ContactName,
            Phone       = request.Phone,
            Email       = request.Email,
            Address     = request.Address,
            Notes       = request.Notes,
            CreatedBy   = _tenant.UserId,
        };

        var created = await _repo.CreateAsync(supplier);
        return Created($"api/v1/suppliers/{created.Id}", ApiResponse<Supplier>.Ok(created, "Proveedor creado"));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplierRequest request)
    {
        var supplier = new Supplier
        {
            Id          = id,
            CompanyId   = _tenant.CompanyId,
            Name        = request.Name,
            ContactName = request.ContactName,
            Phone       = request.Phone,
            Email       = request.Email,
            Address     = request.Address,
            Notes       = request.Notes,
        };

        var updated = await _repo.UpdateAsync(supplier);
        if (updated is null)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));
        return Ok(ApiResponse<Supplier>.Ok(updated, "Proveedor actualizado"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _repo.SoftDeleteAsync(id, _tenant.CompanyId);
        if (!deleted)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));
        return Ok(ApiResponse.Ok("Proveedor eliminado"));
    }

    [HttpPost("{id:long}/products")]
    public async Task<IActionResult> AddProduct(long id, [FromBody] AddSupplierProductRequest request)
    {
        var sp = new SupplierProduct
        {
            SupplierId  = id,
            ProductId   = request.ProductId,
            SupplierSku = request.SupplierSku,
            UnitCost    = request.UnitCost,
            LeadTimeDays = request.LeadTimeDays,
            Notes       = request.Notes,
        };
        var result = await _repo.AddSupplierProductAsync(sp);
        return Ok(ApiResponse<SupplierProduct>.Ok(result, "Producto asociado"));
    }

    [HttpDelete("{id:long}/products/{productId:long}")]
    public async Task<IActionResult> RemoveProduct(long id, long productId)
    {
        var removed = await _repo.RemoveSupplierProductAsync(id, productId);
        if (!removed)
            return NotFound(ApiResponse.Fail("Asociacion no encontrada"));
        return Ok(ApiResponse.Ok("Producto desasociado"));
    }

    [HttpGet("{id:long}/suggested-order")]
    public async Task<IActionResult> GetSuggestedOrder(long id)
    {
        var supplier = await _repo.GetByIdAsync(id, _tenant.CompanyId);
        if (supplier is null)
            return NotFound(ApiResponse.Fail("Proveedor no encontrado"));

        var items = (await _repo.GetLowStockItemsForSupplierAsync(id, _tenant.CompanyId, _tenant.BranchId ?? 0)).ToList();

        var response = new SuggestedOrderResponse
        {
            SupplierId = id,
            SupplierName = supplier.Name,
            Items = items,
            TotalEstimatedCost = items.Sum(i => i.EstimatedCost ?? 0),
        };

        return Ok(ApiResponse<SuggestedOrderResponse>.Ok(response));
    }
}
