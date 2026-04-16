using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Finance;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _financeService;
    private readonly ITenantContext _tenant;

    public FinanceController(IFinanceService financeService, ITenantContext tenant)
    {
        _financeService = financeService;
        _tenant = tenant;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] string? type)
    {
        var categories = (await _financeService.GetCategoriesAsync(_tenant.CompanyId, type)).ToList();
        return Ok(ApiResponse<List<FinancialCategory>>.Ok(categories, count: categories.Count));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateFinancialCategoryRequest request)
    {
        var branchId = request.BranchId ?? _tenant.BranchId;
        var created = await _financeService.CreateCategoryAsync(_tenant.CompanyId, _tenant.UserId, branchId, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FinancialCategory>.Ok(created, "Item financiero creado exitosamente"));
    }

    [HttpPut("categories/{id:long}")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpdateFinancialCategoryRequest request)
    {
        var branchId = request.BranchId ?? _tenant.BranchId;
        var updated = await _financeService.UpdateCategoryAsync(_tenant.CompanyId, branchId, id, request);
        return Ok(ApiResponse<FinancialCategory>.Ok(updated, "Item financiero actualizado exitosamente"));
    }

    [HttpDelete("categories/{id:long}")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        await _financeService.DeleteCategoryAsync(_tenant.CompanyId, id);
        return Ok(ApiResponse.Ok("Item financiero eliminado exitosamente"));
    }

    [HttpPost("month/init")]
    public async Task<IActionResult> InitMonth([FromBody] InitFinanceMonthRequest request)
    {
        var branch = request.BranchId ?? _tenant.BranchId;
        var inserted = await _financeService.InitMonthAsync(_tenant.CompanyId, _tenant.UserId, branch, request);
        return Ok(ApiResponse<int>.Ok(inserted, "Mes iniciado exitosamente"));
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries([FromQuery] long? branchId, [FromQuery] string? type, [FromQuery] long? categoryId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var branch = branchId ?? _tenant.BranchId;
        var entries = (await _financeService.GetEntriesAsync(_tenant.CompanyId, branch, type, categoryId, startDate, endDate)).ToList();
        return Ok(ApiResponse<List<FinancialEntry>>.Ok(entries, count: entries.Count));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] CreateFinancialEntryRequest request)
    {
        var branchId = request.BranchId ?? _tenant.BranchId;
        var created = await _financeService.CreateEntryAsync(_tenant.CompanyId, _tenant.UserId, branchId, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FinancialEntry>.Ok(created, "Movimiento creado exitosamente"));
    }

    [HttpPut("entries/{id:long}")]
    public async Task<IActionResult> UpdateEntry(long id, [FromBody] UpdateFinancialEntryRequest request)
    {
        var branchId = request.BranchId ?? _tenant.BranchId;
        var updated = await _financeService.UpdateEntryAsync(_tenant.CompanyId, branchId, id, request);
        return Ok(ApiResponse<FinancialEntry>.Ok(updated, "Movimiento actualizado exitosamente"));
    }

    [HttpDelete("entries/{id:long}")]
    public async Task<IActionResult> DeleteEntry(long id)
    {
        await _financeService.DeleteEntryAsync(_tenant.CompanyId, id);
        return Ok(ApiResponse.Ok("Movimiento eliminado exitosamente"));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var branch = branchId ?? _tenant.BranchId;
        var summary = await _financeService.GetSummaryAsync(_tenant.CompanyId, branch, startDate, endDate);
        return Ok(ApiResponse<FinancialSummary>.Ok(summary));
    }
}
