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

    [HttpGet("templates")]
    public async Task<IActionResult> GetRecurringTemplates([FromQuery] long? branchId, [FromQuery] string? type)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;
        var templates = (await _repository.GetRecurringTemplatesAsync(companyId, branch, type)).ToList();
        return Ok(ApiResponse<List<FinancialRecurringTemplate>>.Ok(templates, count: templates.Count));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateRecurringTemplate([FromBody] CreateFinancialRecurringTemplateRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branch = request.BranchId ?? _tenant.BranchId;

        if (request.Type is not ("income" or "expense"))
            return BadRequest(ApiResponse.Fail("El tipo debe ser income o expense"));
        if (request.DefaultAmount <= 0)
            return BadRequest(ApiResponse.Fail("El monto debe ser mayor a cero"));
        if (request.CategoryId <= 0)
            return BadRequest(ApiResponse.Fail("La categoria es obligatoria"));
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(ApiResponse.Fail("La descripcion es obligatoria"));
        if (request.DayOfMonth < 1 || request.DayOfMonth > 31)
            return BadRequest(ApiResponse.Fail("El dia del mes debe estar entre 1 y 31"));
        if (request.Nature is not ("fixed" or "variable" or "unique"))
            return BadRequest(ApiResponse.Fail("La naturaleza debe ser fixed, variable o unique"));
        if (request.Frequency is not ("weekly" or "biweekly" or "quincenal" or "monthly" or "unique"))
            return BadRequest(ApiResponse.Fail("La periodicidad debe ser weekly, biweekly/quincenal, monthly o unique"));

        if (request.Nature == "variable")
        {
            // Variable templates are allowed for classification, but they are not auto-generated.
            // Frequency is kept but not used by InitMonth.
        }

        if (request.Frequency is "biweekly" or "quincenal")
        {
            if (!request.BiweeklyDay1.HasValue || !request.BiweeklyDay2.HasValue)
                return BadRequest(ApiResponse.Fail("Para quincenal debes definir dos dias del mes"));
            if (request.BiweeklyDay1.Value < 1 || request.BiweeklyDay1.Value > 31 || request.BiweeklyDay2.Value < 1 || request.BiweeklyDay2.Value > 31)
                return BadRequest(ApiResponse.Fail("Los dias quincenales deben estar entre 1 y 31"));
            if (request.BiweeklyDay1.Value == request.BiweeklyDay2.Value)
                return BadRequest(ApiResponse.Fail("Los dias quincenales deben ser diferentes"));
        }

        var category = await _repository.GetCategoryByIdAsync(request.CategoryId, companyId);
        if (category is null)
            return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("La categoria no coincide con el tipo seleccionado"));

        var created = await _repository.CreateRecurringTemplateAsync(new FinancialRecurringTemplate
        {
            CompanyId = companyId,
            BranchId = branch,
            CategoryId = request.CategoryId,
            Type = request.Type.Trim().ToLowerInvariant(),
            Description = request.Description.Trim(),
            DefaultAmount = Math.Round(request.DefaultAmount, 2),
            DayOfMonth = request.DayOfMonth,
            Nature = request.Nature.Trim().ToLowerInvariant(),
            Frequency = request.Frequency.Trim().ToLowerInvariant(),
            BiweeklyDay1 = request.BiweeklyDay1,
            BiweeklyDay2 = request.BiweeklyDay2,
            IsActive = request.IsActive,
            CreatedBy = userId,
        });

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<FinancialRecurringTemplate>.Ok(created, "Plantilla creada exitosamente"));
    }

    [HttpPut("templates/{id:long}")]
    public async Task<IActionResult> UpdateRecurringTemplate(long id, [FromBody] UpdateFinancialRecurringTemplateRequest request)
    {
        var companyId = _tenant.CompanyId;
        var existing = await _repository.GetRecurringTemplateByIdAsync(id, companyId);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Plantilla no encontrada"));

        if (request.Type is not ("income" or "expense"))
            return BadRequest(ApiResponse.Fail("El tipo debe ser income o expense"));
        if (request.DefaultAmount <= 0)
            return BadRequest(ApiResponse.Fail("El monto debe ser mayor a cero"));
        if (request.CategoryId <= 0)
            return BadRequest(ApiResponse.Fail("La categoria es obligatoria"));
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(ApiResponse.Fail("La descripcion es obligatoria"));
        if (request.DayOfMonth < 1 || request.DayOfMonth > 31)
            return BadRequest(ApiResponse.Fail("El dia del mes debe estar entre 1 y 31"));

        var category = await _repository.GetCategoryByIdAsync(request.CategoryId, companyId);
        if (category is null)
            return NotFound(ApiResponse.Fail("Categoria no encontrada"));
        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("La categoria no coincide con el tipo seleccionado"));

        existing.BranchId = request.BranchId ?? _tenant.BranchId;
        existing.CategoryId = request.CategoryId;
        existing.Type = request.Type.Trim().ToLowerInvariant();
        existing.Description = request.Description.Trim();
        existing.DefaultAmount = Math.Round(request.DefaultAmount, 2);
        existing.DayOfMonth = request.DayOfMonth;
        existing.Nature = request.Nature.Trim().ToLowerInvariant();
        existing.Frequency = request.Frequency.Trim().ToLowerInvariant();
        existing.BiweeklyDay1 = request.BiweeklyDay1;
        existing.BiweeklyDay2 = request.BiweeklyDay2;
        existing.IsActive = request.IsActive;

        var updated = await _repository.UpdateRecurringTemplateAsync(existing);
        return Ok(ApiResponse<FinancialRecurringTemplate>.Ok(updated, "Plantilla actualizada exitosamente"));
    }

    [HttpDelete("templates/{id:long}")]
    public async Task<IActionResult> DeleteRecurringTemplate(long id)
    {
        var companyId = _tenant.CompanyId;
        await _repository.SoftDeleteRecurringTemplateAsync(id, companyId);
        return Ok(ApiResponse.Ok("Plantilla eliminada exitosamente"));
    }

    [HttpPost("month/init")]
    public async Task<IActionResult> InitMonth([FromBody] InitFinanceMonthRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branch = request.BranchId ?? _tenant.BranchId;

        if (string.IsNullOrWhiteSpace(request.Month))
            return BadRequest(ApiResponse.Fail("El mes es obligatorio (YYYY-MM)"));

        if (!DateTime.TryParse($"{request.Month}-01", out var monthStart))
            return BadRequest(ApiResponse.Fail("Formato de mes invalido. Usa YYYY-MM"));

        var inserted = await _repository.InitMonthFromRecurringTemplatesAsync(companyId, branch, monthStart, userId);
        return Ok(ApiResponse<int>.Ok(inserted, "Mes iniciado exitosamente"));
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
            Status = string.IsNullOrWhiteSpace(request.Status) ? "posted" : request.Status.Trim().ToLowerInvariant(),
            OccurrenceInMonth = request.OccurrenceInMonth ?? 1,
            IsManual = request.IsManual ?? true,
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
        if (!string.IsNullOrWhiteSpace(request.Status))
            existing.Status = request.Status.Trim().ToLowerInvariant();
        if (request.OccurrenceInMonth.HasValue)
            existing.OccurrenceInMonth = request.OccurrenceInMonth.Value;
        if (request.IsManual.HasValue)
            existing.IsManual = request.IsManual.Value;

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
