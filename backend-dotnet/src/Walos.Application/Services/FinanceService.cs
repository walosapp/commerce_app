using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Finance;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class FinanceService : IFinanceService
{
    private readonly IFinanceRepository _repository;
    private readonly ILogger<FinanceService> _logger;

    public FinanceService(IFinanceRepository repository, ILogger<FinanceService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<FinancialCategory>> GetCategoriesAsync(long companyId, string? type = null)
    {
        return await _repository.GetCategoriesAsync(companyId, type);
    }

    public async Task<FinancialCategory> CreateCategoryAsync(long companyId, long userId, long? branchId, CreateFinancialCategoryRequest request)
    {
        ValidateCategoryRequest(request);

        var created = await _repository.CreateCategoryAsync(new FinancialCategory
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            Type = request.Type.Trim().ToLowerInvariant(),
            ColorHex = request.ColorHex,
            DefaultAmount = Math.Round(request.DefaultAmount, 2),
            DayOfMonth = request.DayOfMonth,
            Nature = request.Nature.Trim().ToLowerInvariant(),
            Frequency = request.Frequency.Trim().ToLowerInvariant(),
            BiweeklyDay1 = request.BiweeklyDay1,
            BiweeklyDay2 = request.BiweeklyDay2,
            AutoIncludeInMonth = request.AutoIncludeInMonth,
            IsSystem = false,
            IsActive = request.IsActive,
            CreatedBy = userId,
        });

        return created;
    }

    public async Task<FinancialCategory> UpdateCategoryAsync(long companyId, long? branchId, long categoryId, UpdateFinancialCategoryRequest request)
    {
        var category = await _repository.GetCategoryByIdAsync(categoryId, companyId)
            ?? throw new NotFoundException("Item financiero no encontrado");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre del item es obligatorio");
        if (request.Type is not ("income" or "expense"))
            throw new ValidationException("El tipo debe ser income o expense");
        if (request.DefaultAmount <= 0)
            throw new ValidationException("El monto debe ser mayor a cero");
        if (request.DayOfMonth < 1 || request.DayOfMonth > 31)
            throw new ValidationException("El dia del mes debe estar entre 1 y 31");

        category.BranchId = branchId;
        category.Name = request.Name.Trim();
        category.Type = request.Type.Trim().ToLowerInvariant();
        category.ColorHex = request.ColorHex;
        category.DefaultAmount = Math.Round(request.DefaultAmount, 2);
        category.DayOfMonth = request.DayOfMonth;
        category.Nature = request.Nature.Trim().ToLowerInvariant();
        category.Frequency = request.Frequency.Trim().ToLowerInvariant();
        category.BiweeklyDay1 = request.BiweeklyDay1;
        category.BiweeklyDay2 = request.BiweeklyDay2;
        category.AutoIncludeInMonth = request.AutoIncludeInMonth;
        category.IsActive = request.IsActive;

        return await _repository.UpdateCategoryAsync(category);
    }

    public async Task DeleteCategoryAsync(long companyId, long categoryId)
    {
        await _repository.SoftDeleteCategoryAsync(categoryId, companyId);
    }

    public async Task<int> InitMonthAsync(long companyId, long userId, long? branchId, InitFinanceMonthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Month))
            throw new ValidationException("El mes es obligatorio (YYYY-MM)");

        if (!DateTime.TryParse($"{request.Month}-01", out var monthStart))
            throw new ValidationException("Formato de mes invalido. Usa YYYY-MM");

        return await _repository.InitMonthFromFinancialItemsAsync(companyId, branchId, monthStart, userId);
    }

    public async Task<IEnumerable<FinancialEntry>> GetEntriesAsync(long companyId, long? branchId, string? type, long? categoryId, DateTime? startDate, DateTime? endDate)
    {
        return await _repository.GetEntriesAsync(companyId, branchId, type, categoryId, startDate, endDate);
    }

    public async Task<FinancialEntry> CreateEntryAsync(long companyId, long userId, long? branchId, CreateFinancialEntryRequest request)
    {
        ValidateEntryRequest(request);

        var category = await _repository.GetCategoryByIdAsync(request.CategoryId, companyId)
            ?? throw new NotFoundException("Categoria no encontrada");

        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("La categoria no coincide con el tipo seleccionado");

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
        return created;
    }

    public async Task<FinancialEntry> UpdateEntryAsync(long companyId, long? branchId, long entryId, UpdateFinancialEntryRequest request)
    {
        var existing = await _repository.GetEntryByIdAsync(entryId, companyId)
            ?? throw new NotFoundException("Movimiento no encontrado");

        ValidateEntryRequest(request);

        var category = await _repository.GetCategoryByIdAsync(request.CategoryId, companyId)
            ?? throw new NotFoundException("Categoria no encontrada");

        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("La categoria no coincide con el tipo seleccionado");

        existing.BranchId = branchId;
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

        return await _repository.UpdateEntryAsync(existing);
    }

    public async Task DeleteEntryAsync(long companyId, long entryId)
    {
        var existing = await _repository.GetEntryByIdAsync(entryId, companyId)
            ?? throw new NotFoundException("Movimiento no encontrado");

        if (!existing.IsManual)
        {
            existing.Status = "skipped";
            await _repository.UpdateEntryAsync(existing);
            return;
        }

        await _repository.SoftDeleteEntryAsync(entryId, companyId);
    }

    public async Task<FinancialSummary> GetSummaryAsync(long companyId, long? branchId, DateTime? startDate, DateTime? endDate)
    {
        return await _repository.GetSummaryAsync(companyId, branchId, startDate, endDate);
    }

    private static void ValidateCategoryRequest(CreateFinancialCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre del item es obligatorio");
        if (request.Type is not ("income" or "expense"))
            throw new ValidationException("El tipo debe ser income o expense");
        if (request.DefaultAmount <= 0)
            throw new ValidationException("El monto debe ser mayor a cero");
        if (request.DayOfMonth < 1 || request.DayOfMonth > 31)
            throw new ValidationException("El dia del mes debe estar entre 1 y 31");
        if (request.Nature is not ("fixed" or "variable" or "unique"))
            throw new ValidationException("La naturaleza debe ser fixed, variable o unique");
        if (request.Frequency is not ("weekly" or "biweekly" or "quincenal" or "monthly" or "unique"))
            throw new ValidationException("La periodicidad debe ser weekly, biweekly/quincenal, monthly o unique");
        if (request.Frequency is "biweekly" or "quincenal")
        {
            if (!request.BiweeklyDay1.HasValue || !request.BiweeklyDay2.HasValue)
                throw new ValidationException("Para quincenal debes definir dos dias del mes");
            if (request.BiweeklyDay1.Value == request.BiweeklyDay2.Value)
                throw new ValidationException("Los dias quincenales deben ser diferentes");
        }
    }

    private static void ValidateEntryRequest(CreateFinancialEntryRequest request)
    {
        if (request.Type is not ("income" or "expense"))
            throw new ValidationException("El tipo debe ser income o expense");
        if (request.Amount <= 0)
            throw new ValidationException("El monto debe ser mayor a cero");
        if (request.CategoryId <= 0)
            throw new ValidationException("La categoria es obligatoria");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("La descripcion es obligatoria");
    }
}
