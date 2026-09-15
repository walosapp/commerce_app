using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize]
[RequireFeature(WalosFeatures.Purchases)]
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
    [Authorize(Policy = WalosPolicies.PurchasesRead)]
    public async Task<IActionResult> GetAll([FromQuery] long? supplierId = null)
    {
        var items = (await _service.GetAllAsync(_tenant.CompanyId, _tenant.BranchId, supplierId)).ToList();
        return Ok(ApiResponse<IEnumerable<PurchaseOrderResponse>>.Ok(items, count: items.Count));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = WalosPolicies.PurchasesRead)]
    public async Task<IActionResult> GetById(long id)
    {
        var order = await _service.GetByIdAsync(id, _tenant.CompanyId, _tenant.BranchId);
        if (order is null) return NotFound(ApiResponse.Fail("Pedido no encontrado"));
        return Ok(ApiResponse<PurchaseOrderResponse>.Ok(order));
    }

    [HttpPost]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        if (_tenant.BranchId.HasValue && request.BranchId != _tenant.BranchId.Value)
            throw new ValidationException("La sucursal del pedido no coincide con la sucursal autenticada");

        // Company-wide users may select a branch; the repository validates it inside
        // the same transaction as the inserts.
        var branchId = _tenant.BranchId ?? request.BranchId;
        var order = await _service.CreateAsync(_tenant.CompanyId, branchId, _tenant.UserId, request);
        return Ok(ApiResponse<PurchaseOrderResponse>.Ok(order, "Pedido creado exitosamente"));
    }

    [HttpPost("{id:long}/receive")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
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
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> Cancel(long id)
    {
        var ok = await _service.CancelAsync(id, _tenant.CompanyId, _tenant.BranchId);
        if (!ok) return BadRequest(ApiResponse.Fail("No se puede cancelar. El pedido ya fue recibido o no existe."));
        return Ok(ApiResponse.Ok("Pedido cancelado"));
    }
}
