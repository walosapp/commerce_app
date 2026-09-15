using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Application.Security;
using Walos.Domain.Entities;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales")]
[Authorize(Policy = WalosPolicies.SalesOperator)]
[RequireAnyFeature(WalosFeatures.Restaurant, WalosFeatures.Pos)]
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
    [RequireFeature(WalosFeatures.Restaurant)]
    public async Task<IActionResult> GetTables([FromQuery] long? branchId)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);

        var tables = (await _salesService.GetActiveTablesAsync(_tenant.CompanyId, branch.Value)).ToList();
        return Ok(ApiResponse<List<SalesTable>>.Ok(tables, count: tables.Count));
    }

    [HttpPost("tables")]
    [RequireFeature(WalosFeatures.Restaurant)]
    [Authorize(Policy = WalosPolicies.SalesOperator)]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);

        var result = await _salesService.CreateTableAsync(_tenant.CompanyId, branch.Value, _tenant.UserId, request);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<SalesTable>.Ok(result.Table, "Mesa creada exitosamente"));
    }

    [HttpPost("tables/{id:long}/invoice")]
    [RequireFeature(WalosFeatures.Restaurant)]
    [Authorize(Policy = WalosPolicies.SalesOperator)]
    public async Task<IActionResult> InvoiceTable(long id, [FromBody] InvoiceTableRequest? request, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);

        request ??= new InvoiceTableRequest();
        var result = await _salesService.InvoiceTableAsync(_tenant.CompanyId, branch.Value, _tenant.UserId, id, request);
        return Ok(ApiResponse<InvoiceResult>.Ok(result, "Mesa facturada exitosamente"));
    }

    [HttpDelete("tables/{id:long}")]
    [RequireFeature(WalosFeatures.Restaurant)]
    [Authorize(Policy = WalosPolicies.SalesOperator)]
    public async Task<IActionResult> CancelTable(long id, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId);
        await _salesService.CancelTableAsync(_tenant.CompanyId, branch, id);
        return Ok(ApiResponse.Ok("Mesa cancelada"));
    }

    [HttpPatch("items/{itemId:long}/quantity")]
    [RequireFeature(WalosFeatures.Restaurant)]
    [Authorize(Policy = WalosPolicies.SalesOperator)]
    public async Task<IActionResult> UpdateItemQuantity(long itemId, [FromBody] UpdateItemQuantityRequest request, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);
        await _salesService.UpdateItemQuantityAsync(_tenant.CompanyId, branch.Value, itemId, request);
        return Ok(ApiResponse.Ok("Cantidad actualizada"));
    }

    [HttpPost("tables/{id:long}/items")]
    [RequireFeature(WalosFeatures.Restaurant)]
    [Authorize(Policy = WalosPolicies.SalesOperator)]
    public async Task<IActionResult> AddItemsToTable(long id, [FromBody] List<CreateTableItemDto> items, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);

        await _salesService.AddItemsToTableAsync(_tenant.CompanyId, branch.Value, id, items);
        return Ok(ApiResponse.Ok("Productos agregados exitosamente"));
    }

    [HttpPatch("tables/{id:long}/name")]
    [RequireFeature(WalosFeatures.Restaurant)]
    [Authorize(Policy = WalosPolicies.SalesOperator)]
    public async Task<IActionResult> RenameTable(long id, [FromBody] RenameTableRequest request, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId);
        await _salesService.RenameTableAsync(_tenant.CompanyId, branch, id, request.Name ?? string.Empty);
        return Ok(ApiResponse.Ok("Mesa renombrada"));
    }

    [HttpGet("orders/{id:long}/items")]
    public async Task<IActionResult> GetOrderItems(long id, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId);
        var items = (await _salesService.GetOrderItemsAsync(_tenant.CompanyId, branch, id)).ToList();
        return Ok(ApiResponse<List<OrderItem>>.Ok(items, count: items.Count));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId, [FromQuery] string? date)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);
        var parsedDate = DateTime.TryParse(date, out var d) ? d.Date : DateTime.UtcNow.Date;
        var (dateFrom, dateTo) = await GetBusinessDayRangeAsync(parsedDate);
        var summary = await _salesRepo.GetSalesSummaryAsync(_tenant.CompanyId, branch.Value, dateFrom, dateTo);
        return Ok(ApiResponse<SalesSummary>.Ok(summary));
    }

    [HttpGet("orders/completed")]
    public async Task<IActionResult> GetCompletedOrders([FromQuery] long? branchId, [FromQuery] string? date)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);
        var parsedDate = DateTime.TryParse(date, out var d) ? d.Date : DateTime.UtcNow.Date;
        var (dateFrom, dateTo) = await GetBusinessDayRangeAsync(parsedDate);
        var orders = (await _salesRepo.GetCompletedOrdersAsync(_tenant.CompanyId, branch.Value, dateFrom, dateTo)).ToList();
        return Ok(ApiResponse<List<CompletedOrder>>.Ok(orders, count: orders.Count));
    }

    [HttpGet("orders/{id:long}/receipt")]
    public async Task<IActionResult> GetReceipt(long id, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId);
        var result = await _salesService.GetReceiptAsync(_tenant.CompanyId, branch, id);
        return Ok(ApiResponse<ReceiptData>.Ok(result));
    }

    [HttpGet("orders/{id:long}/kitchen")]
    [RequireFeature(WalosFeatures.Restaurant)]
    public async Task<IActionResult> GetKitchenTicket(long id, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId);
        var result = await _salesService.GetKitchenTicketAsync(_tenant.CompanyId, branch, id);
        return Ok(ApiResponse<KitchenTicketData>.Ok(result));
    }

    [HttpGet("orders/search")]
    public async Task<IActionResult> SearchOrders(
        [FromQuery] long? branchId,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? status,
        [FromQuery] string? refundStatus,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? search,
        [FromQuery] decimal? minTotal,
        [FromQuery] decimal? maxTotal,
        [FromQuery] string sortBy = "created_at",
        [FromQuery] string sortDir = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);
        var request = new OrderSearchRequest
        {
            BranchId = branch.Value,
            DateFrom = DateTime.TryParse(dateFrom, out var df) ? df : null,
            DateTo = DateTime.TryParse(dateTo, out var dt) ? dt.Date.AddDays(1).AddTicks(-1) : null,
            Status = status,
            RefundStatus = refundStatus,
            PaymentMethod = paymentMethod,
            Search = search,
            MinTotal = minTotal,
            MaxTotal = maxTotal,
            Page = page,
            Limit = Math.Min(limit, 100),
            SortBy = sortBy,
            SortDir = sortDir
        };

        var (items, totalCount) = await _salesService.SearchOrdersAsync(_tenant.CompanyId, request);
        return Ok(ApiResponse<List<OrderDetailResponse>>.Ok(items, count: totalCount));
    }

    [HttpGet("orders/{id:long}/detail")]
    public async Task<IActionResult> GetOrderDetail(long id, [FromQuery] long? branchId = null)
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId);
        var result = await _salesService.GetOrderDetailAsync(_tenant.CompanyId, branch, id);
        return Ok(ApiResponse<OrderDetailResponse>.Ok(result));
    }

    [HttpGet("orders/export")]
    public async Task<IActionResult> ExportOrders(
        [FromQuery] long? branchId,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? status,
        [FromQuery] string? refundStatus,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? search,
        [FromQuery] decimal? minTotal,
        [FromQuery] decimal? maxTotal,
        [FromQuery] string sortBy = "created_at",
        [FromQuery] string sortDir = "desc")
    {
        var branch = await _salesService.ResolveBranchAsync(
            _tenant.CompanyId, _tenant.BranchId, branchId, required: true);
        var request = new OrderSearchRequest
        {
            BranchId = branch.Value,
            DateFrom = DateTime.TryParse(dateFrom, out var df) ? df : null,
            DateTo = DateTime.TryParse(dateTo, out var dt) ? dt.Date.AddDays(1).AddTicks(-1) : null,
            Status = status,
            RefundStatus = refundStatus,
            PaymentMethod = paymentMethod,
            Search = search,
            MinTotal = minTotal,
            MaxTotal = maxTotal,
            SortBy = sortBy,
            SortDir = sortDir
        };

        var csv = await _salesService.ExportOrdersCsvAsync(_tenant.CompanyId, request);
        return File(csv, "text/csv", $"ventas-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

public record RenameTableRequest(string? Name);
