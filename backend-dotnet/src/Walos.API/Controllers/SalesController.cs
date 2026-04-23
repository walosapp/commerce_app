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
    private readonly ISalesRepository _salesRepo;
    private readonly ISalesService _salesService;
    private readonly ITenantContext _tenant;
    private readonly ICompanyRepository _companyRepo;

    public SalesController(ISalesService salesService, ITenantContext tenant, ISalesRepository salesRepo, ICompanyRepository companyRepo)
    {
        _salesService = salesService;
        _tenant = tenant;
        _salesRepo = salesRepo;
        _companyRepo = companyRepo;
    }

    private async Task<(DateTime dateFrom, DateTime dateTo)> GetBusinessDayRangeAsync(DateTime requestedDate)
    {
        var settings = await _companyRepo.GetCompanySettingsAsync(_tenant.CompanyId);
        var openTime  = settings?.BusinessOpenTime  ?? TimeSpan.Zero;
        var closeTime = settings?.BusinessCloseTime ?? new TimeSpan(23, 59, 59);

        var crossesMidnight = closeTime <= openTime;

        DateTime dateFrom;
        DateTime dateTo;

        if (!crossesMidnight)
        {
            dateFrom = requestedDate.Date + openTime;
            dateTo   = requestedDate.Date + closeTime;
        }
        else
        {
            dateFrom = requestedDate.Date + openTime;
            dateTo   = requestedDate.Date.AddDays(1) + closeTime;
        }

        return (dateFrom, dateTo);
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

    [HttpPatch("tables/{id:long}/name")]
    public async Task<IActionResult> RenameTable(long id, [FromBody] RenameTableRequest request)
    {
        await _salesService.RenameTableAsync(_tenant.CompanyId, id, request.Name ?? string.Empty);
        return Ok(ApiResponse.Ok("Mesa renombrada"));
    }

    [HttpGet("orders/{id:long}/items")]
    public async Task<IActionResult> GetOrderItems(long id)
    {
        var items = (await _salesRepo.GetOrderItemsAsync(id, _tenant.CompanyId)).ToList();
        return Ok(ApiResponse<List<OrderItem>>.Ok(items, count: items.Count));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long branchId, [FromQuery] string? date)
    {
        var parsedDate = DateTime.TryParse(date, out var d) ? d.Date : DateTime.UtcNow.Date;
        var (dateFrom, dateTo) = await GetBusinessDayRangeAsync(parsedDate);
        var summary = await _salesRepo.GetSalesSummaryAsync(_tenant.CompanyId, branchId, dateFrom, dateTo);
        return Ok(ApiResponse<SalesSummary>.Ok(summary));
    }

    [HttpGet("orders/completed")]
    public async Task<IActionResult> GetCompletedOrders([FromQuery] long branchId, [FromQuery] string? date)
    {
        var parsedDate = DateTime.TryParse(date, out var d) ? d.Date : DateTime.UtcNow.Date;
        var (dateFrom, dateTo) = await GetBusinessDayRangeAsync(parsedDate);
        var orders = (await _salesRepo.GetCompletedOrdersAsync(_tenant.CompanyId, branchId, dateFrom, dateTo)).ToList();
        return Ok(ApiResponse<List<CompletedOrder>>.Ok(orders, count: orders.Count));
    }
}

public record RenameTableRequest(string? Name);
