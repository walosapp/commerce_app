using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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

        await _authRepo.ResetFailedLoginAsync(user.Id);
        await _authRepo.UpdateLastLoginAsync(user.Id, ipAddress);

        var tokenString = GenerateJwtToken(user);

        var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        await _authRepo.SaveRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(refreshDays));

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

        var tokenString = GenerateJwtToken(user);
        var newRefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        await _authRepo.SaveRefreshTokenAsync(user.Id, newRefreshToken, DateTime.UtcNow.AddDays(refreshDays));

        return new TokenResult
        {
            Token = tokenString,
            RefreshToken = newRefreshToken
        };
    }

    public async Task LogoutAsync(long userId)
    {
        await _authRepo.SaveRefreshTokenAsync(userId, string.Empty, DateTime.UtcNow.AddDays(-1));
        _logger.LogInformation("Logout exitoso: UserId {UserId}", userId);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSecret = _configuration["Jwt:Secret"]!;
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim("userId", user.Id.ToString()),
            new Claim("companyId", user.CompanyId.ToString()),
            new Claim("branchId", user.BranchId?.ToString() ?? ""),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.RoleCode ?? "user")
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
        AvatarUrl = user.AvatarUrl
    };
}
