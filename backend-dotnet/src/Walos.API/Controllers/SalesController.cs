using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;
    private readonly ITenantContext _tenant;

    public SalesController(ISalesService salesService, ITenantContext tenant)
    {
        _salesService = salesService;
        _tenant = tenant;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables([FromQuery] long? branchId)
    {
        var branch = branchId ?? _tenant.BranchId;

        if (branch is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var tables = (await _salesService.GetActiveTablesAsync(_tenant.CompanyId, branch.Value)).ToList();
        return Ok(ApiResponse<List<SalesTable>>.Ok(tables, count: tables.Count));
    }

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        var branchId = _tenant.BranchId;
        if (branchId is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var result = await _salesService.CreateTableAsync(_tenant.CompanyId, branchId.Value, _tenant.UserId, request);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<SalesTable>.Ok(result.Table, "Mesa creada exitosamente"));
    }

    [HttpPost("tables/{id:long}/invoice")]
    public async Task<IActionResult> InvoiceTable(long id, [FromBody] InvoiceTableRequest? request)
    {
        var branchId = _tenant.BranchId;
        if (branchId is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        request ??= new InvoiceTableRequest();
        var result = await _salesService.InvoiceTableAsync(_tenant.CompanyId, branchId.Value, _tenant.UserId, id, request);
        return Ok(ApiResponse<InvoiceResult>.Ok(result, "Mesa facturada exitosamente"));
    }

    [HttpDelete("tables/{id:long}")]
    public async Task<IActionResult> CancelTable(long id)
    {
        await _salesService.CancelTableAsync(_tenant.CompanyId, id);
        return Ok(ApiResponse.Ok("Mesa cancelada"));
    }

    [HttpPatch("items/{itemId:long}/quantity")]
    public async Task<IActionResult> UpdateItemQuantity(long itemId, [FromBody] UpdateItemQuantityRequest request)
    {
        var branchId = _tenant.BranchId ?? 0;
        await _salesService.UpdateItemQuantityAsync(_tenant.CompanyId, branchId, itemId, request);
        return Ok(ApiResponse.Ok("Cantidad actualizada"));
    }

    [HttpPost("tables/{id:long}/items")]
    public async Task<IActionResult> AddItemsToTable(long id, [FromBody] List<CreateTableItemDto> items)
    {
        await _salesService.AddItemsToTableAsync(_tenant.CompanyId, id, items);
        return Ok(ApiResponse.Ok("Productos agregados exitosamente"));
    }
}
