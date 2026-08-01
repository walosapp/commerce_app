using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;
    private readonly ITenantContext _tenant;

    public PurchaseOrdersController(IPurchaseOrderService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] long? supplierId = null)
    {
        var items = (await _service.GetAllAsync(_tenant.CompanyId, supplierId)).ToList();
        return Ok(ApiResponse<IEnumerable<PurchaseOrderResponse>>.Ok(items, count: items.Count));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var order = await _service.GetByIdAsync(id, _tenant.CompanyId);
        if (order is null) return NotFound(ApiResponse.Fail("Pedido no encontrado"));
        return Ok(ApiResponse<PurchaseOrderResponse>.Ok(order));
    }

    [HttpPost]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        var order = await _service.CreateAsync(_tenant.CompanyId, _tenant.UserId, request);
        return Ok(ApiResponse<PurchaseOrderResponse>.Ok(order, "Pedido creado exitosamente"));
    }

    [HttpPost("{id:long}/receive")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> Receive(long id, [FromBody] ReceivePurchaseOrderRequest request)
    {
        try
        {
            var userId = _tenant.UserId;
            var branchId = _tenant.BranchId ?? throw new InvalidOperationException("No hay sucursal en contexto");
            var order = await _service.ReceiveAsync(id, _tenant.CompanyId, branchId, userId, request);
            return Ok(ApiResponse<PurchaseOrderResponse>.Ok(order, "Pedido recibido. Stock e inventario actualizados."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> Cancel(long id)
    {
        var ok = await _service.CancelAsync(id, _tenant.CompanyId);
        if (!ok) return BadRequest(ApiResponse.Fail("No se puede cancelar. El pedido ya fue recibido o no existe."));
        return Ok(ApiResponse.Ok("Pedido cancelado"));
    }
}
