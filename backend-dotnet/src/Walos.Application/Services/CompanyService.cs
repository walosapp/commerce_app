using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Company;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class CompanyService : ICompanyService
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "light", "dark", "grayscale", "neon", "pink", "purple", "glass"
    };

    private static readonly HashSet<string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly ICompanyRepository _repository;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(ICompanyRepository repository, ILogger<CompanyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CompanySettings> GetSettingsAsync(long companyId)
    {
        var settings = await _repository.GetCompanySettingsAsync(companyId)
            ?? throw new NotFoundException("Empresa no encontrada");

        settings.DisplayName = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? settings.Name
            : settings.DisplayName;

        return settings;
    }

    public async Task<CompanySettings> UpdateSettingsAsync(long companyId, long userId, UpdateCompanySettingsRequest request)
    {
        var settings = await _repository.GetCompanySettingsAsync(companyId)
            ?? throw new NotFoundException("Empresa no encontrada");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ValidationException("El nombre del negocio es obligatorio");

        if (!AllowedThemes.Contains(request.ThemePreference))
            throw new ValidationException("Tema no permitido");

        settings.Name = request.DisplayName.Trim();
        settings.DisplayName = request.DisplayName.Trim();
        settings.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        settings.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        settings.ThemePreference = request.ThemePreference.Trim().ToLowerInvariant();
        settings.UpdatedBy = userId;

        if (TimeSpan.TryParseExact(request.BusinessOpenTime, @"hh\:mm", null, out var openTime))
            settings.BusinessOpenTime = openTime;
        if (TimeSpan.TryParseExact(request.BusinessCloseTime, @"hh\:mm", null, out var closeTime))
            settings.BusinessCloseTime = closeTime;

        var updated = await _repository.UpdateCompanySettingsAsync(settings);

        _logger.LogInformation("Configuracion actualizada para empresa {CompanyId} por usuario {UserId}", companyId, userId);

        return updated;
    }

    public async Task<CompanyOperationsSettings> GetOperationsSettingsAsync(long companyId)
    {
        return await _repository.GetCompanyOperationsSettingsAsync(companyId)
            ?? throw new NotFoundException("Empresa no encontrada");
    }

    public async Task<CompanyOperationsSettings> UpdateOperationsSettingsAsync(long companyId, UpdateCompanyOperationsSettingsRequest request)
    {
        if (request.MaxDiscountPercent < 0 || request.MaxDiscountPercent > 100)
            throw new ValidationException("El porcentaje maximo debe estar entre 0 y 100");

        if (request.MaxDiscountAmount < 0)
            throw new ValidationException("El valor maximo de descuento no puede ser negativo");

        if (request.DiscountOverrideThresholdPercent < 0 || request.DiscountOverrideThresholdPercent > request.MaxDiscountPercent)
            throw new ValidationException("El umbral de confirmacion debe estar entre 0 y el maximo porcentual");

        var updated = await _repository.UpdateCompanyOperationsSettingsAsync(new CompanyOperationsSettings
        {
            CompanyId = companyId,
            ManualDiscountEnabled = request.ManualDiscountEnabled,
            MaxDiscountPercent = Math.Round(request.MaxDiscountPercent, 2),
            MaxDiscountAmount = Math.Round(request.MaxDiscountAmount, 2),
            DiscountRequiresOverride = request.DiscountRequiresOverride,
            DiscountOverrideThresholdPercent = Math.Round(request.DiscountOverrideThresholdPercent, 2)
        });

        _logger.LogInformation("Reglas operativas actualizadas para empresa {CompanyId}", companyId);

        return updated;
    }

    public async Task<string> UploadLogoAsync(long companyId, long userId, Stream fileStream, string fileName, string contentType)
    {
        if (!AllowedLogoContentTypes.Contains(contentType))
            throw new ValidationException("Formato no permitido. Use JPG, PNG o WebP");

        var company = await _repository.GetCompanySettingsAsync(companyId)
            ?? throw new NotFoundException("Empresa no encontrada");

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "branding");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var newFileName = $"company_{companyId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
        var filePath = Path.Combine(uploadsDir, newFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(stream);
        }

        var logoUrl = $"/uploads/branding/{newFileName}";
        await _repository.UpdateCompanyLogoAsync(companyId, logoUrl, userId);

        _logger.LogInformation("Logo actualizado para empresa {CompanyId}", companyId);

        return logoUrl;
    }
}
