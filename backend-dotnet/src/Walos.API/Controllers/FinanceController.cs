using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Finance;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/finance")]
[Authorize]
public class FinanceController : ControllerBase
{
    private readonly IFinanceRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(IFinanceRepository repository, ITenantContext tenant, ILogger<FinanceController> logger)
    {
        _repository = repository;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] string? type)
    {
        var companyId = _tenant.CompanyId;
        var categories = (await _repository.GetCategoriesAsync(companyId, type)).ToList();
        return Ok(ApiResponse<List<FinancialCategory>>.Ok(categories, count: categories.Count));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateFinancialCategoryRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre de la categoria es obligatorio"));
        if (request.Type is not ("income" or "expense"))
            return BadRequest(ApiResponse.Fail("El tipo debe ser income o expense"));

        var created = await _repository.CreateCategoryAsync(new FinancialCategory
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            Type = request.Type.Trim().ToLowerInvariant(),
            ColorHex = request.ColorHex,
            IsSystem = false,
            IsActive = true,
            CreatedBy = userId,
        });

        return StatusCode(StatusCodes.Status201Created, ApiResponse<FinancialCategory>.Ok(created, "Categoria creada exitosamente"));
    }

    [HttpPut("categories/{id:long}")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpdateFinancialCategoryRequest request)
    {
        var companyId = _tenant.CompanyId;
        var category = await _repository.GetCategoryByIdAsync(id, companyId);
        if (category is null)
            return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre de la categoria es obligatorio"));
        if (request.Type is not ("income" or "expense"))
            return BadRequest(ApiResponse.Fail("El tipo debe ser income o expense"));

        category.Name = request.Name.Trim();
        category.Type = request.Type.Trim().ToLowerInvariant();
        category.ColorHex = request.ColorHex;

        var updated = await _repository.UpdateCategoryAsync(category);
        return Ok(ApiResponse<FinancialCategory>.Ok(updated, "Categoria actualizada exitosamente"));
    }

    [HttpDelete("categories/{id:long}")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        var companyId = _tenant.CompanyId;
        await _repository.SoftDeleteCategoryAsync(id, companyId);
        return Ok(ApiResponse.Ok("Categoria eliminada exitosamente"));
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries([FromQuery] long? branchId, [FromQuery] string? type, [FromQuery] long? categoryId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;
        var entries = (await _repository.GetEntriesAsync(companyId, branch, type, categoryId, startDate, endDate)).ToList();
        return Ok(ApiResponse<List<FinancialEntry>>.Ok(entries, count: entries.Count));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] CreateFinancialEntryRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branchId = request.BranchId ?? _tenant.BranchId;

        if (request.Type is not ("income" or "expense"))
            return BadRequest(ApiResponse.Fail("El tipo debe ser income o expense"));
        if (request.Amount <= 0)
            return BadRequest(ApiResponse.Fail("El monto debe ser mayor a cero"));
        if (request.CategoryId <= 0)
            return BadRequest(ApiResponse.Fail("La categoria es obligatoria"));
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(ApiResponse.Fail("La descripcion es obligatoria"));

        var category = await _repository.GetCategoryByIdAsync(request.CategoryId, companyId);
        if (category is null)
            return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("La categoria no coincide con el tipo seleccionado"));

        var created = await _repository.CreateEntryAsync(new FinancialEntry
        {
            CompanyId = companyId,
            BranchId = branchId,
            CategoryId = request.CategoryId,
            Type = request.Type.Trim().ToLowerInvariant(),
            Description = request.Description.Trim(),
            Amount = Math.Round(request.Amount, 2),
            EntryDate = request.EntryDate == default ? DateTime.UtcNow : request.EntryDate,
            Nature = request.Nature,
            Frequency = request.Frequency,
            Notes = request.Notes,
            CreatedBy = userId,
        });

        _logger.LogInformation("Movimiento financiero creado: {Description}, EntryId: {EntryId}", created.Description, created.Id);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FinancialEntry>.Ok(created, "Movimiento creado exitosamente"));
    }

    [HttpPut("entries/{id:long}")]
    public async Task<IActionResult> UpdateEntry(long id, [FromBody] UpdateFinancialEntryRequest request)
    {
        var companyId = _tenant.CompanyId;
        var existing = await _repository.GetEntryByIdAsync(id, companyId);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Movimiento no encontrado"));
        if (request.Type is not ("income" or "expense"))
            return BadRequest(ApiResponse.Fail("El tipo debe ser income o expense"));
        if (request.Amount <= 0)
            return BadRequest(ApiResponse.Fail("El monto debe ser mayor a cero"));
        if (request.CategoryId <= 0)
            return BadRequest(ApiResponse.Fail("La categoria es obligatoria"));
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(ApiResponse.Fail("La descripcion es obligatoria"));

        var category = await _repository.GetCategoryByIdAsync(request.CategoryId, companyId);
        if (category is null)
            return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("La categoria no coincide con el tipo seleccionado"));

        existing.BranchId = request.BranchId ?? _tenant.BranchId;
        existing.CategoryId = request.CategoryId;
        existing.Type = request.Type.Trim().ToLowerInvariant();
        existing.Description = request.Description.Trim();
        existing.Amount = Math.Round(request.Amount, 2);
        existing.EntryDate = request.EntryDate == default ? existing.EntryDate : request.EntryDate;
        existing.Nature = request.Nature;
        existing.Frequency = request.Frequency;
        existing.Notes = request.Notes;

        var updated = await _repository.UpdateEntryAsync(existing);
        return Ok(ApiResponse<FinancialEntry>.Ok(updated, "Movimiento actualizado exitosamente"));
    }

    [HttpDelete("entries/{id:long}")]
    public async Task<IActionResult> DeleteEntry(long id)
    {
        var companyId = _tenant.CompanyId;
        await _repository.SoftDeleteEntryAsync(id, companyId);
        return Ok(ApiResponse.Ok("Movimiento eliminado exitosamente"));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;
        var summary = await _repository.GetSummaryAsync(companyId, branch, startDate, endDate);
        return Ok(ApiResponse<FinancialSummary>.Ok(summary));
    }
}
