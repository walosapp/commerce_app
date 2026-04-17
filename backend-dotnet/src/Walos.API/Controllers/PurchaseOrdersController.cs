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
    private readonly IPurchaseOrderRepository _repo;
    private readonly ITenantContext _tenant;

    public PurchaseOrdersController(IPurchaseOrderRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] long? supplierId = null)
    {
        var items = (await _repo.GetAllAsync(_tenant.CompanyId, supplierId)).ToList();
        return Ok(ApiResponse<IEnumerable<PurchaseOrderResponse>>.Ok(items, count: items.Count));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var order = await _repo.GetByIdAsync(id, _tenant.CompanyId);
        if (order is null) return NotFound(ApiResponse.Fail("Pedido no encontrado"));
        return Ok(ApiResponse<PurchaseOrderResponse>.Ok(order));
    }

    [HttpPost]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        if (request.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("El pedido debe tener al menos un producto"));

        var userId = _tenant.UserId;
        var order = await _repo.CreateAsync(_tenant.CompanyId, userId, request);
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
            var order = await _repo.ReceiveAsync(id, _tenant.CompanyId, branchId, userId, request);
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
        var ok = await _repo.CancelAsync(id, _tenant.CompanyId);
        if (!ok) return BadRequest(ApiResponse.Fail("No se puede cancelar. El pedido ya fue recibido o no existe."));
        return Ok(ApiResponse.Ok("Pedido cancelado"));
    }
}
