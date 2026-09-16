using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Admin;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;
using Walos.Application.Security;

namespace Walos.Application.Services;

public class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepo;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IAdminRepository adminRepo, ILogger<AdminService> logger)
    {
        _adminRepo = adminRepo;
        _logger = logger;
    }

    public async Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            throw new ValidationException("El nombre del comercio es requerido");

        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            throw new ValidationException("El email del administrador es requerido");

        PasswordPolicy.Validate(request.AdminPassword);

        if (string.IsNullOrWhiteSpace(request.BranchName))
            throw new ValidationException("El nombre de la sucursal es requerido");

        if (!string.IsNullOrWhiteSpace(request.TaxId) && await _adminRepo.TaxIdExistsAsync(request.TaxId))
            throw new BusinessException("Ya existe un comercio registrado con ese NIT/RUT");

        if (await _adminRepo.EmailExistsAsync(request.AdminEmail))
            throw new BusinessException("Ya existe un usuario registrado con ese email");

        return await _adminRepo.CreateTenantAsync(request);
    }

    public Task<IEnumerable<TenantResponse>> GetTenantsAsync()
        => _adminRepo.GetTenantsAsync();

    public Task<TenantResponse?> GetTenantByIdAsync(long companyId)
        => _adminRepo.GetTenantByIdAsync(companyId);

    public async Task<bool> SetTenantActiveAsync(long companyId, bool isActive)
    {
        var exists = await _adminRepo.GetTenantByIdAsync(companyId);
        if (exists is null)
            throw new BusinessException("Comercio no encontrado");

        if (IsSystemTenant(exists) && !isActive)
            throw new BusinessException(
                "El comercio de sistema no se puede desactivar",
                "system_tenant_protected");

        return await _adminRepo.SetTenantActiveAsync(companyId, isActive);
    }

    public async Task<TenantResponse?> UpdateTenantAsync(long companyId, UpdateTenantRequest request)
    {
        var exists = await _adminRepo.GetTenantByIdAsync(companyId);
        if (exists is null)
            throw new BusinessException("Comercio no encontrado");

        if (IsSystemTenant(exists)
            && request.TaxId is not null
            && !string.Equals(
                request.TaxId.Trim(),
                WalosSystemIdentity.CompanyTaxId,
                StringComparison.Ordinal))
        {
            throw new BusinessException(
                "El identificador fiscal del comercio de sistema no se puede modificar",
                "system_tenant_protected");
        }

        if (IsSystemTenant(exists) && request.TaxId is not null)
            request.TaxId = WalosSystemIdentity.CompanyTaxId;

        var normalizedAdminEmail = string.IsNullOrWhiteSpace(request.AdminEmail)
            ? null
            : request.AdminEmail.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedAdminEmail)
            && !string.Equals(normalizedAdminEmail, exists.AdminEmail, StringComparison.OrdinalIgnoreCase)
            && await _adminRepo.AdminEmailExistsAsync(normalizedAdminEmail, companyId))
        {
            throw new BusinessException("Ya existe otro administrador con ese email");
        }

        request.AdminEmail = normalizedAdminEmail;

        return await _adminRepo.UpdateTenantAsync(companyId, request);
    }

    public async Task<long> ResetTenantAdminPasswordAsync(long companyId, long actorUserId, string newPassword)
    {
        PasswordPolicy.Validate(newPassword);

        var exists = await _adminRepo.GetTenantByIdAsync(companyId);
        if (exists is null)
            throw new BusinessException("Comercio no encontrado");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var targetUserId = await _adminRepo.ResetTenantAdminPasswordAsync(companyId, actorUserId, passwordHash);
        if (!targetUserId.HasValue)
            throw new BusinessException("El comercio debe tener exactamente un administrador activo distinto del actor");

        _logger.LogWarning(
            "PasswordReset ActorUserId {ActorUserId} TargetUserId {TargetUserId} CompanyId {CompanyId}; target sessions invalidated",
            actorUserId,
            targetUserId.Value,
            companyId);
        return targetUserId.Value;
    }

    public async Task<IReadOnlyList<BranchAdminResponse>> GetBranchesAsync(long companyId)
    {
        if (await _adminRepo.GetTenantByIdAsync(companyId) is null)
            throw new NotFoundException("Comercio no encontrado");
        return await _adminRepo.GetBranchesAsync(companyId);
    }

    public async Task<BranchAdminResponse> CreateBranchAsync(
        long companyId, CreateBranchAdminRequest request, long actorId)
    {
        ValidateRequired(request.Name, "nombre", 200);
        ValidateRequired(request.Code, "codigo", 20);
        ValidateRequired(request.BranchType, "tipo", 50);
        ValidateRequired(request.Address, "direccion", 500);
        ValidateRequired(request.City, "ciudad", 100);
        ValidateRequired(request.Country, "pais", 2, exactLength: true);
        ValidateOptional(request.Email, "email", 100);
        ValidateOptional(request.Phone, "telefono", 20);
        ValidateOptional(request.State, "departamento", 100);
        ValidateOptional(request.PostalCode, "codigo postal", 10);
        ValidateCapacity(request.MaxTables, "maxTables");
        ValidateCapacity(request.MaxCapacity, "maxCapacity");

        Normalize(request);
        return await _adminRepo.CreateBranchAsync(companyId, request, actorId);
    }

    public async Task<BranchAdminResponse> UpdateBranchAsync(
        long companyId, long branchId, UpdateBranchAdminRequest request, long actorId)
    {
        if (branchId <= 0)
            throw new ValidationException("Sucursal invalida");
        if (request.Name is null && request.Code is null && request.BranchType is null
            && request.Email is null && request.Phone is null && request.Address is null
            && request.City is null && request.State is null && request.Country is null
            && request.PostalCode is null && request.MaxTables is null
            && request.MaxCapacity is null && request.IsActive is null)
        {
            throw new ValidationException("Debe especificar al menos un cambio para la sucursal");
        }
        ValidateRequired(request.Name ?? string.Empty, "nombre", 200);
        ValidateRequired(request.Code ?? string.Empty, "codigo", 20);
        ValidateRequired(request.BranchType ?? string.Empty, "tipo", 50);
        ValidateRequired(request.Address ?? string.Empty, "direccion", 500);
        ValidateRequired(request.City ?? string.Empty, "ciudad", 100);
        ValidateRequired(request.Country ?? string.Empty, "pais", 2, exactLength: true);
        if (!request.IsActive.HasValue)
            throw new ValidationException("El campo isActive es obligatorio");
        ValidateOptional(request.Email, "email", 100);
        ValidateOptional(request.Phone, "telefono", 20);
        ValidateOptional(request.State, "departamento", 100);
        ValidateOptional(request.PostalCode, "codigo postal", 10);
        ValidateCapacity(request.MaxTables, "maxTables");
        ValidateCapacity(request.MaxCapacity, "maxCapacity");

        Normalize(request);
        return await _adminRepo.UpdateBranchAsync(companyId, branchId, request, actorId)
            ?? throw new NotFoundException("Sucursal no encontrada");
    }

    private static void ValidateRequired(string? value, string field, int maxLength, bool exactLength = false)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ValidationException($"El campo {field} es obligatorio");
        if ((exactLength && normalized.Length != maxLength) || (!exactLength && normalized.Length > maxLength))
            throw new ValidationException($"El campo {field} tiene una longitud invalida");
    }

    private static void ValidateCapacity(int? value, string field)
    {
        if (value.HasValue && value.Value <= 0)
            throw new ValidationException($"{field} debe ser mayor que cero");
    }

    private static void ValidateOptional(string? value, string field, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
            throw new ValidationException($"El campo {field} supera la longitud maxima");
    }

    private static void Normalize(CreateBranchAdminRequest request)
    {
        request.Name = request.Name?.Trim() ?? string.Empty;
        request.Code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        request.BranchType = request.BranchType?.Trim().ToLowerInvariant() ?? string.Empty;
        request.Address = request.Address?.Trim() ?? string.Empty;
        request.City = request.City?.Trim() ?? string.Empty;
        request.Country = request.Country?.Trim().ToUpperInvariant() ?? string.Empty;
        request.Email = NormalizeOptional(request.Email);
        request.Phone = NormalizeOptional(request.Phone);
        request.State = NormalizeOptional(request.State);
        request.PostalCode = NormalizeOptional(request.PostalCode);
    }

    private static void Normalize(UpdateBranchAdminRequest request)
    {
        request.Name = request.Name?.Trim();
        request.Code = request.Code?.Trim().ToUpperInvariant();
        request.BranchType = request.BranchType?.Trim().ToLowerInvariant();
        request.Address = request.Address?.Trim();
        request.City = request.City?.Trim();
        request.Country = request.Country?.Trim().ToUpperInvariant();
        request.Email = NormalizeOptional(request.Email);
        request.Phone = NormalizeOptional(request.Phone);
        request.State = NormalizeOptional(request.State);
        request.PostalCode = NormalizeOptional(request.PostalCode);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsSystemTenant(TenantResponse tenant) =>
        string.Equals(
            tenant.TaxId,
            WalosSystemIdentity.CompanyTaxId,
            StringComparison.Ordinal);
}

