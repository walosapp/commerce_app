using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales/cash-register")]
[Authorize(Policy = WalosPolicies.CashOperator)]
public class CashRegisterController : ControllerBase
{
    private readonly ICashRegisterService _cashRegisterService;
    private readonly ICompanyRepository _companyRepo;
    private readonly ITenantContext _tenant;

    public CashRegisterController(ICashRegisterService cashRegisterService, ICompanyRepository companyRepo, ITenantContext tenant)
    {
        _cashRegisterService = cashRegisterService;
        _companyRepo = companyRepo;
        _tenant = tenant;
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashRegisterRequest request)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var register = await _cashRegisterService.OpenAsync(
            _tenant.CompanyId, _tenant.BranchId.Value, _tenant.UserId, request);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CashRegisterResponse>.Ok(register, "Caja abierta exitosamente"));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var register = await _cashRegisterService.GetActiveAsync(
            _tenant.CompanyId, _tenant.BranchId.Value, _tenant.UserId);

        if (register == null)
            return Ok(ApiResponse<CashRegisterResponse?>.Ok(null, "No tienes una caja abierta"));

        return Ok(ApiResponse<CashRegisterResponse>.Ok(register));
    }

    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, [FromBody] CloseCashRegisterRequest request)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var register = await _cashRegisterService.CloseAsync(
            id, _tenant.CompanyId, _tenant.BranchId.Value, _tenant.UserId, request);

        return Ok(ApiResponse<CashRegisterResponse>.Ok(register, "Caja cerrada exitosamente"));
    }

    [HttpPost("{id:long}/movement")]
    public async Task<IActionResult> AddMovement(long id, [FromBody] CashMovementRequest request)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var movement = await _cashRegisterService.AddMovementAsync(
            id, _tenant.CompanyId, _tenant.BranchId.Value, _tenant.UserId, request);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CashMovementResponse>.Ok(movement, "Movimiento registrado exitosamente"));
    }

    [HttpGet("{id:long}/movements")]
    public async Task<IActionResult> GetMovements(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var movements = (await _cashRegisterService.GetMovementsAsync(id, _tenant.CompanyId, _tenant.BranchId.Value)).ToList();
        return Ok(ApiResponse<List<CashMovementResponse>>.Ok(movements, count: movements.Count));
    }

    [HttpGet("{id:long}/summary")]
    public async Task<IActionResult> GetSummary(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var summary = await _cashRegisterService.GetSummaryAsync(id, _tenant.CompanyId, _tenant.BranchId.Value);
        return Ok(ApiResponse<CashRegisterSummaryResponse>.Ok(summary));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var (items, totalCount) = await _cashRegisterService.GetHistoryAsync(
            _tenant.CompanyId, _tenant.BranchId.Value, dateFrom, dateTo, page, limit);

        var list = items.ToList();
        return Ok(ApiResponse<List<CashRegisterResponse>>.Ok(list, count: totalCount));
    }

    [HttpGet("{id:long}/z-report")]
    public async Task<IActionResult> GetZReport(long id)
    {
        if (!_tenant.BranchId.HasValue)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var summary = await _cashRegisterService.GetSummaryAsync(id, _tenant.CompanyId, _tenant.BranchId.Value);
        var company = await _companyRepo.GetCompanySettingsAsync(_tenant.CompanyId);

        var zReport = new ZReportData(
            CashRegisterId: summary.Register.Id,
            OpenedAt: summary.Register.OpenedAt,
            ClosedAt: summary.Register.ClosedAt,
            OpenedByName: summary.Register.OpenedByName ?? "Cajero",
            ClosedByName: summary.Register.ClosedByName,
            OpeningAmount: summary.Register.OpeningAmount,
            ClosingAmount: summary.Register.ClosingAmount,
            ExpectedCash: summary.Register.ExpectedCash,
            Difference: summary.Register.Difference,
            TotalSales: summary.Register.TotalSales,
            OrderCount: summary.Register.OrderCount,
            TotalCashSales: summary.Register.TotalCashSales,
            TotalCardSales: summary.Register.TotalCardSales,
            TotalTransferSales: summary.Register.TotalTransferSales,
            TotalOtherSales: summary.Register.TotalOtherSales,
            TotalDiscounts: summary.Register.TotalDiscounts,
            TotalCredits: summary.Register.TotalCredits,
            TotalTips: summary.Register.TotalTips,
            CashIn: summary.Register.CashIn,
            CashOut: summary.Register.CashOut,
            PaymentBreakdown: summary.PaymentBreakdown,
            Movements: summary.Movements,
            CompanyName: company?.Name ?? "Empresa",
            CompanyLegalName: company?.LegalName
        );

        return Ok(ApiResponse<ZReportData>.Ok(zReport));
    }
}
