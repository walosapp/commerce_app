using Walos.Application.DTOs.Users;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;

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

    public async Task<User> CreateAsync(long companyId, CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException("Nombre y apellido son requeridos");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("El email es requerido");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ValidationException("La contraseña debe tener al menos 6 caracteres");

        if (await _repository.EmailExistsAsync(request.Email))
            throw new BusinessException("Ya existe un usuario con ese email");

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

    public async Task<User?> UpdateAsync(long companyId, long id, UpdateUserRequest request)
    {
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

    public async Task<bool> SetStatusAsync(long companyId, long currentUserId, long id, bool isActive)
    {
        if (id == currentUserId)
            throw new ValidationException("No puedes desactivar tu propio usuario");

        return await _repository.SetActiveAsync(id, companyId, isActive);
    }

    public async Task<bool> DeleteAsync(long companyId, long currentUserId, long id)
    {
        if (id == currentUserId)
            throw new ValidationException("No puedes eliminar tu propio usuario");

        return await _repository.SoftDeleteAsync(id, companyId);
    }
}

