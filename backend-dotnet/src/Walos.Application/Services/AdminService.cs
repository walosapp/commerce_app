using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Admin;
using Walos.Domain.Exceptions;

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

        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 6)
            throw new ValidationException("La contrasena del administrador debe tener al menos 6 caracteres");

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

        return await _adminRepo.SetTenantActiveAsync(companyId, isActive);
    }

    public async Task<TenantResponse?> UpdateTenantAsync(long companyId, UpdateTenantRequest request)
    {
        var exists = await _adminRepo.GetTenantByIdAsync(companyId);
        if (exists is null)
            throw new BusinessException("Comercio no encontrado");

        return await _adminRepo.UpdateTenantAsync(companyId, request);
    }
}
