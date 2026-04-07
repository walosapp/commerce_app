using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Company;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/company")]
[Authorize]
public class CompanyController : ControllerBase
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "light",
        "dark",
        "grayscale",
        "neon",
        "pink",
        "purple"
    };

    private readonly ICompanyRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CompanyController> _logger;

    public CompanyController(ICompanyRepository repository, ITenantContext tenant, ILogger<CompanyController> logger)
    {
        _repository = repository;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var companyId = _tenant.CompanyId;
        var settings = await _repository.GetCompanySettingsAsync(companyId);

        if (settings is null)
            return NotFound(ApiResponse.Fail("Empresa no encontrada"));

        settings.DisplayName = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? settings.Name
            : settings.DisplayName;

        return Ok(ApiResponse<CompanySettings>.Ok(settings));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateCompanySettingsRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        var settings = await _repository.GetCompanySettingsAsync(companyId);
        if (settings is null)
            return NotFound(ApiResponse.Fail("Empresa no encontrada"));

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(ApiResponse.Fail("El nombre del negocio es obligatorio"));

        if (!AllowedThemes.Contains(request.ThemePreference))
            return BadRequest(ApiResponse.Fail("Tema no permitido"));

        settings.Name = request.DisplayName.Trim();
        settings.DisplayName = request.DisplayName.Trim();
        settings.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        settings.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        settings.ThemePreference = request.ThemePreference.Trim().ToLowerInvariant();
        settings.UpdatedBy = userId;

        var updated = await _repository.UpdateCompanySettingsAsync(settings);

        _logger.LogInformation("Configuracion actualizada para empresa {CompanyId} por usuario {UserId}", companyId, userId);

        return Ok(ApiResponse<CompanySettings>.Ok(updated, "Configuracion actualizada exitosamente"));
    }

    [HttpGet("settings/operations")]
    public async Task<IActionResult> GetOperationsSettings()
    {
        var companyId = _tenant.CompanyId;
        var settings = await _repository.GetCompanyOperationsSettingsAsync(companyId);

        if (settings is null)
            return NotFound(ApiResponse.Fail("Empresa no encontrada"));

        return Ok(ApiResponse<CompanyOperationsSettings>.Ok(settings));
    }

    [HttpPut("settings/operations")]
    public async Task<IActionResult> UpdateOperationsSettings([FromBody] UpdateCompanyOperationsSettingsRequest request)
    {
        var companyId = _tenant.CompanyId;

        if (request.MaxDiscountPercent < 0 || request.MaxDiscountPercent > 100)
            return BadRequest(ApiResponse.Fail("El porcentaje maximo debe estar entre 0 y 100"));

        if (request.MaxDiscountAmount < 0)
            return BadRequest(ApiResponse.Fail("El valor maximo de descuento no puede ser negativo"));

        if (request.DiscountOverrideThresholdPercent < 0 || request.DiscountOverrideThresholdPercent > request.MaxDiscountPercent)
            return BadRequest(ApiResponse.Fail("El umbral de confirmacion debe estar entre 0 y el maximo porcentual"));

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

        return Ok(ApiResponse<CompanyOperationsSettings>.Ok(updated, "Reglas operativas actualizadas exitosamente"));
    }

    [HttpPost("settings/logo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No se proporciono archivo"));

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse.Fail("Formato no permitido. Use JPG, PNG o WebP"));

        var company = await _repository.GetCompanySettingsAsync(companyId);
        if (company is null)
            return NotFound(ApiResponse.Fail("Empresa no encontrada"));

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "branding");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"company_{companyId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var logoUrl = $"/uploads/branding/{fileName}";
        await _repository.UpdateCompanyLogoAsync(companyId, logoUrl, userId);

        _logger.LogInformation("Logo actualizado para empresa {CompanyId}", companyId);

        return Ok(ApiResponse<object>.Ok(new { logoUrl }, "Logo actualizado exitosamente"));
    }
}
