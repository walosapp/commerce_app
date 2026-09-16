using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Walos.Application.Security;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private static readonly HashSet<string> CanonicalRoles = new(StringComparer.Ordinal)
    {
        WalosRoles.Dev,
        WalosRoles.PlatformAdmin,
        WalosRoles.SuperAdmin,
        WalosRoles.Manager,
        WalosRoles.Cashier,
        WalosRoles.Waiter,
    };

    public AuthService(IAuthRepository authRepo, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _authRepo = authRepo;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Usuario y contraseña son requeridos");

        var user = await _authRepo.GetUserByEmailAsync(username);

        if (user is null && !username.Contains('@'))
        {
            _logger.LogWarning("Login fallido — usuario no encontrado: {Username}", username);
            throw new BusinessException("Credenciales inválidas");
        }

        if (user is null)
        {
            _logger.LogWarning("Login fallido — email no encontrado: {Email}", username);
            throw new BusinessException("Credenciales inválidas");
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            _logger.LogWarning("Login bloqueado para {Email} — cuenta bloqueada por {Minutes} min", user.Email, remaining);
            throw new BusinessException($"Cuenta bloqueada. Intenta en {remaining} minutos");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login fallido — cuenta inactiva: {Email}", user.Email);
            throw new BusinessException("Cuenta desactivada. Contacta al administrador");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            await _authRepo.IncrementFailedLoginAsync(user.Id);

            if (user.FailedLoginAttempts + 1 >= MaxFailedAttempts)
            {
                await _authRepo.LockUserAsync(user.Id, DateTime.UtcNow.AddMinutes(LockoutMinutes));
                _logger.LogWarning("Cuenta bloqueada por intentos fallidos: {Email}", user.Email);
                throw new BusinessException($"Cuenta bloqueada por {LockoutMinutes} minutos tras {MaxFailedAttempts} intentos fallidos");
            }

            _logger.LogWarning("Login fallido — contraseña incorrecta: {Email}", user.Email);
            throw new BusinessException("Credenciales inválidas");
        }

        EnsureCanonicalRole(user);

        await _authRepo.ResetFailedLoginAsync(user.Id);
        await _authRepo.UpdateLastLoginAsync(user.Id, ipAddress);

        var tokenString = GenerateJwtToken(user);

        var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        if (!await _authRepo.SaveRefreshTokenAfterPasswordVerificationAsync(
                user.Id,
                user.PasswordHash,
                refreshToken,
                DateTime.UtcNow.AddDays(refreshDays)))
            throw new BusinessException("Credentials changed during login; retry with the current password");

        _logger.LogInformation("Login exitoso: {Email}, CompanyId: {CompanyId}", user.Email, user.CompanyId);

        return new LoginResult
        {
            Token = tokenString,
            RefreshToken = refreshToken,
            User = MapUserInfo(user)
        };
    }

    public async Task<TokenResult> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ValidationException("Refresh token requerido");

        var user = await _authRepo.GetUserByRefreshTokenAsync(refreshToken)
            ?? throw new BusinessException("Refresh token inválido o expirado");

        if (!user.IsActive)
            throw new BusinessException("Cuenta desactivada");

        EnsureCanonicalRole(user);

        var newRefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        if (!await _authRepo.RotateRefreshTokenAsync(
                user.Id,
                refreshToken,
                newRefreshToken,
                DateTime.UtcNow.AddDays(refreshDays)))
            throw new BusinessException("Refresh token invalid or expired");

        return new TokenResult
        {
            Token = GenerateJwtToken(user),
            RefreshToken = newRefreshToken
        };
    }

    public async Task LogoutAsync(long userId)
    {
        await _authRepo.SaveRefreshTokenAsync(userId, string.Empty, DateTime.UtcNow.AddDays(-1));
        _logger.LogInformation("Logout exitoso: UserId {UserId}", userId);
    }

    public async Task<TokenResult> ChangePasswordAsync(
        long userId,
        long companyId,
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            throw new ValidationException("La contraseña actual es requerida");
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            throw new ValidationException("La confirmación de contraseña no coincide");

        PasswordPolicy.Validate(newPassword);

        var user = await _authRepo.GetUserForPasswordChangeAsync(userId, companyId)
            ?? throw new BusinessException("No fue posible cambiar la contraseña");
        EnsureCanonicalRole(user);

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new BusinessException("La contraseña actual es incorrecta");
        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
            throw new ValidationException("La nueva contraseña debe ser diferente a la actual");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        if (!await _authRepo.ChangePasswordAndRotateRefreshTokenAsync(
                userId,
                companyId,
                user.PasswordHash,
                passwordHash,
                refreshToken,
                DateTime.UtcNow.AddDays(refreshDays)))
            throw new BusinessException("No fue posible cambiar la contraseña");

        user.PasswordHash = passwordHash;
        _logger.LogInformation(
            "PasswordChanged ActorUserId {ActorUserId} TargetUserId {TargetUserId} CompanyId {CompanyId}; prior sessions invalidated",
            userId,
            userId,
            companyId);

        return new TokenResult
        {
            Token = GenerateJwtToken(user),
            RefreshToken = refreshToken
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSecret = _configuration["Jwt:Secret"]!;
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(WalosClaimTypes.UserId, user.Id.ToString()),
            new Claim(WalosClaimTypes.CompanyId, user.CompanyId.ToString()),
            new Claim(WalosClaimTypes.BranchId, user.BranchId?.ToString() ?? ""),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.RoleCode ?? "user"),
            new Claim(
                WalosClaimTypes.PlatformAdmin,
                IsTrustedPlatformAdmin(user).ToString().ToLowerInvariant()),
            new Claim(WalosClaimTypes.SecurityStamp, AccessTokenSecurityStamp.Compute(jwtSecret, user))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserInfo MapUserInfo(User user) => new()
    {
        Id = user.Id,
        Name = $"{user.FirstName} {user.LastName}".Trim(),
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Role = user.RoleCode ?? "user",
        RoleName = user.RoleName,
        CompanyId = user.CompanyId,
        CompanyName = user.CompanyName,
        BranchId = user.BranchId,
        BranchName = user.BranchName,
        Language = user.Language,
        AvatarUrl = user.AvatarUrl,
        IsPlatformAdmin = IsTrustedPlatformAdmin(user)
    };

    private static bool IsTrustedPlatformAdmin(User user) =>
        (string.Equals(user.RoleCode, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase)
         || string.Equals(user.RoleCode, WalosRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase))
        && string.Equals(user.CompanyTaxId, WalosSystemIdentity.CompanyTaxId, StringComparison.Ordinal);

    private void EnsureCanonicalRole(User user)
    {
        if (user.RoleCode is not null && CanonicalRoles.Contains(user.RoleCode))
            return;

        _logger.LogWarning(
            "Emision de token rechazada para UserId {UserId}: rol no canonico {RoleCode}",
            user.Id,
            user.RoleCode ?? "<null>");
        throw new BusinessException(
            "La cuenta no tiene un rol autorizado",
            "unsupported_role");
    }
}
