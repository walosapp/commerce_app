using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales/refunds")]
[Authorize(Policy = WalosPolicies.CashOperator)]
public class RefundController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly ITenantContext _tenant;

    public RefundController(IRefundService refundService, ITenantContext tenant)
    {
        _refundService = refundService;
        _tenant = tenant;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] CreateRefundRequest request)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var result = await _refundService.CreateRefundAsync(
            _tenant.CompanyId, _tenant.BranchId.Value, _tenant.UserId, idempotencyKey, request);

        return Ok(ApiResponse<RefundResponse>.Ok(result, "Devolucion creada exitosamente"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var (items, count) = await _refundService.GetAllAsync(
            _tenant.CompanyId, _tenant.BranchId.Value, dateFrom, dateTo, page, limit);

        return Ok(ApiResponse<IEnumerable<RefundResponse>>.Ok(items, count: count));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var result = await _refundService.GetByIdAsync(id, _tenant.CompanyId, _tenant.BranchId.Value);
        if (result == null) return NotFound(ApiResponse.Fail("Devolucion no encontrada"));
        return Ok(ApiResponse<RefundResponse>.Ok(result));
    }

    [HttpGet("by-order/{orderId:long}")]
    public async Task<IActionResult> GetByOrder(long orderId)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var result = await _refundService.GetByOrderIdAsync(orderId, _tenant.CompanyId, _tenant.BranchId.Value);
        return Ok(ApiResponse<IEnumerable<RefundResponse>>.Ok(result));
    }
}
