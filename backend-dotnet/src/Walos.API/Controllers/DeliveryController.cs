using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Delivery;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/delivery")]
[Authorize]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;
    private readonly ITenantContext _tenant;

    public DeliveryController(IDeliveryService deliveryService, ITenantContext tenant)
    {
        _deliveryService = deliveryService;
        _tenant = tenant;
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        var orders = await _deliveryService.GetOrdersAsync(
            _tenant.CompanyId, _tenant.BranchId ?? 0, status, dateFrom, dateTo);
        var list = orders.ToList();
        return Ok(ApiResponse<IEnumerable<DeliveryOrder>>.Ok(list, count: list.Count));
    }

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> GetOrder(long id)
    {
        var order = await _deliveryService.GetOrderByIdAsync(id, _tenant.CompanyId);
        if (order is null)
            return NotFound(ApiResponse.Fail("Pedido no encontrado"));
        return Ok(ApiResponse<DeliveryOrder>.Ok(order));
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateDeliveryOrderRequest request)
    {
        var order = await _deliveryService.CreateOrderAsync(
            _tenant.CompanyId, _tenant.BranchId ?? 0, _tenant.UserId, request);
        return Created($"api/v1/delivery/orders/{order.Id}",
            ApiResponse<DeliveryOrder>.Ok(order, "Pedido creado exitosamente"));
    }

    [HttpPost("orders/{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, [FromBody] ChangeStatusRequest? request)
    {
        await _deliveryService.AcceptOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request?.Comment);
        return Ok(ApiResponse.Ok("Pedido aceptado"));
    }

    [HttpPost("orders/{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] ChangeStatusRequest request)
    {
        await _deliveryService.RejectOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request.Comment ?? string.Empty);
        return Ok(ApiResponse.Ok("Pedido rechazado"));
    }

    [HttpPost("orders/{id:long}/prepare")]
    public async Task<IActionResult> Prepare(long id, [FromBody] ChangeStatusRequest? request)
    {
        await _deliveryService.PrepareOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request?.Comment);
        return Ok(ApiResponse.Ok("Pedido en preparacion"));
    }

    [HttpPost("orders/{id:long}/ready")]
    public async Task<IActionResult> Ready(long id, [FromBody] ChangeStatusRequest? request)
    {
        await _deliveryService.ReadyOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request?.Comment);
        return Ok(ApiResponse.Ok("Pedido listo para despacho"));
    }

    [HttpPost("orders/{id:long}/dispatch")]
    public async Task<IActionResult> Dispatch(long id, [FromBody] ChangeStatusRequest? request)
    {
        await _deliveryService.DispatchOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request?.Comment);
        return Ok(ApiResponse.Ok("Pedido despachado"));
    }

    [HttpPost("orders/{id:long}/deliver")]
    public async Task<IActionResult> Deliver(long id, [FromBody] ChangeStatusRequest? request)
    {
        await _deliveryService.DeliverOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request?.Comment);
        return Ok(ApiResponse.Ok("Pedido entregado"));
    }

    [HttpPost("orders/{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, [FromBody] ChangeStatusRequest request)
    {
        await _deliveryService.CancelOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request.Comment ?? string.Empty);
        return Ok(ApiResponse.Ok("Pedido cancelado"));
    }

    [HttpPost("orders/{id:long}/return")]
    public async Task<IActionResult> Return(long id, [FromBody] ChangeStatusRequest request)
    {
        await _deliveryService.ReturnOrderAsync(id, _tenant.CompanyId, _tenant.UserId, request.Comment ?? string.Empty);
        return Ok(ApiResponse.Ok("Pedido devuelto"));
    }
}
