using Walos.Application.DTOs.Users;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;
using Walos.Application.Security;

namespace Walos.Application.Services;

public class UsersService : IUsersService
{
    private readonly IUsersRepository _repository;

    public UsersService(IUsersRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<User>> GetAllAsync(long companyId)
        => _repository.GetAllAsync(companyId);

    public Task<IEnumerable<RoleOption>> GetRolesAsync(long companyId)
        => _repository.GetRolesAsync(companyId, excludeDev: true);

    public Task<User?> GetByIdAsync(long id, long companyId)
        => _repository.GetByIdAsync(id, companyId);

    public async Task<User> CreateAsync(
        long companyId,
        long currentUserId,
        string currentRole,
        CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException("Nombre y apellido son requeridos");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("El email es requerido");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ValidationException("La contraseña debe tener al menos 6 caracteres");

        if (await _repository.EmailExistsAsync(request.Email))
            throw new BusinessException("Ya existe un usuario con ese email");

        await ValidateAssignmentAsync(companyId, currentUserId, currentRole, request.RoleId, request.BranchId);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User
        {
            CompanyId = companyId,
            BranchId = request.BranchId,
            RoleId = request.RoleId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
        };

        return await _repository.CreateAsync(user, passwordHash);
    }

    public async Task<User?> UpdateAsync(
        long companyId,
        long currentUserId,
        string currentRole,
        long id,
        UpdateUserRequest request)
    {
        var existing = await _repository.GetByIdAsync(id, companyId);
        if (existing is null)
            return null;

        EnsureCanManageUser(currentRole, existing);

        await ValidateAssignmentAsync(companyId, currentUserId, currentRole, request.RoleId, request.BranchId);

        var user = new User
        {
            Id = id,
            CompanyId = companyId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            RoleId = request.RoleId,
            BranchId = request.BranchId,
        };

        return await _repository.UpdateAsync(user);
    }

    private async Task ValidateAssignmentAsync(
        long companyId,
        long currentUserId,
        string currentRole,
        long roleId,
        long? branchId)
    {
        var actorRole = await _repository.GetUserRoleForAssignmentAsync(currentUserId, companyId)
            ?? throw new BusinessException("El rol del usuario autenticado no es valido para este comercio");
        if (!string.Equals(actorRole.Code, currentRole, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("El rol autenticado ya no coincide con el rol actual del usuario");
        var targetRole = await _repository.GetRoleForAssignmentAsync(roleId, companyId)
            ?? throw new ValidationException("El rol seleccionado no pertenece al comercio o esta inactivo");

        if (string.Equals(targetRole.Code, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("El rol de plataforma no puede asignarse desde rutas del comercio");

        if (string.Equals(currentRole, WalosRoles.Manager, StringComparison.OrdinalIgnoreCase)
            && string.Equals(targetRole.Code, WalosRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Un gerente no puede asignar el rol de super administrador");

        if (targetRole.AccessLevel > actorRole.AccessLevel)
            throw new BusinessException("No puedes asignar un rol con nivel superior al propio");

        if (branchId.HasValue
            && !await _repository.IsActiveBranchInCompanyAsync(branchId.Value, companyId))
            throw new ValidationException("La sucursal seleccionada no pertenece al comercio o esta inactiva");
    }

    public async Task<bool> SetStatusAsync(
        long companyId,
        long currentUserId,
        string currentRole,
        long id,
        bool isActive)
    {
        if (id == currentUserId)
            throw new ValidationException("No puedes desactivar tu propio usuario");

        var existing = await _repository.GetByIdAsync(id, companyId);
        if (existing is null) return false;
        EnsureCanManageUser(currentRole, existing);

        return await _repository.SetActiveAsync(id, companyId, isActive);
    }

    public async Task<bool> DeleteAsync(long companyId, long currentUserId, string currentRole, long id)
    {
        if (id == currentUserId)
            throw new ValidationException("No puedes eliminar tu propio usuario");

        var existing = await _repository.GetByIdAsync(id, companyId);
        if (existing is null) return false;
        EnsureCanManageUser(currentRole, existing);

        return await _repository.SoftDeleteAsync(id, companyId);
    }

    private static void EnsureCanManageUser(string currentRole, User target)
    {
        if (string.Equals(currentRole, WalosRoles.Manager, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target.RoleCode, WalosRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Un gerente no puede modificar un super administrador");
    }
}

