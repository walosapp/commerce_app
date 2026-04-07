using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Walos.Application.DTOs.Common;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthRepository _authRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    public AuthController(
        IAuthRepository authRepo,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _authRepo = authRepo;
        _configuration = configuration;
        _logger = logger;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse.Fail("Usuario y contraseña son requeridos"));

        // Username can be email or just a username — we search by email
        var email = request.Username.Contains('@') ? request.Username : request.Username;

        var user = await _authRepo.GetUserByEmailAsync(email);

        // If not found by direct match, try appending common domain (dev convenience)
        if (user is null && !email.Contains('@'))
        {
            // For backwards compatibility: "dev" username won't match email, so skip
            _logger.LogWarning("Login fallido — usuario no encontrado: {Username}", request.Username);
            return Unauthorized(ApiResponse.Fail("Credenciales inválidas"));
        }

        if (user is null)
        {
            _logger.LogWarning("Login fallido — email no encontrado: {Email}", email);
            return Unauthorized(ApiResponse.Fail("Credenciales inválidas"));
        }

        // Check if account is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            _logger.LogWarning("Login bloqueado para {Email} — cuenta bloqueada por {Minutes} min", user.Email, remaining);
            return Unauthorized(ApiResponse.Fail($"Cuenta bloqueada. Intenta en {remaining} minutos"));
        }

        // Check if user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login fallido — cuenta inactiva: {Email}", user.Email);
            return Unauthorized(ApiResponse.Fail("Cuenta desactivada. Contacta al administrador"));
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _authRepo.IncrementFailedLoginAsync(user.Id);

            if (user.FailedLoginAttempts + 1 >= MaxFailedAttempts)
            {
                await _authRepo.LockUserAsync(user.Id, DateTime.UtcNow.AddMinutes(LockoutMinutes));
                _logger.LogWarning("Cuenta bloqueada por intentos fallidos: {Email}", user.Email);
                return Unauthorized(ApiResponse.Fail($"Cuenta bloqueada por {LockoutMinutes} minutos tras {MaxFailedAttempts} intentos fallidos"));
            }

            _logger.LogWarning("Login fallido — contraseña incorrecta: {Email}", user.Email);
            return Unauthorized(ApiResponse.Fail("Credenciales inválidas"));
        }

        // Success — reset failed attempts and update last login
        await _authRepo.ResetFailedLoginAsync(user.Id);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _authRepo.UpdateLastLoginAsync(user.Id, ipAddress);

        // Generate JWT
        var tokenString = GenerateJwtToken(user);

        // Generate refresh token
        var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        await _authRepo.SaveRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(refreshDays));

        _logger.LogInformation("Login exitoso: {Email}, CompanyId: {CompanyId}", user.Email, user.CompanyId);

        return Ok(new
        {
            success = true,
            data = new
            {
                token = tokenString,
                refreshToken,
                user = new
                {
                    id = user.Id,
                    name = $"{user.FirstName} {user.LastName}".Trim(),
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.RoleCode ?? "user",
                    roleName = user.RoleName,
                    companyId = user.CompanyId,
                    companyName = user.CompanyName,
                    branchId = user.BranchId,
                    branchName = user.BranchName,
                    language = user.Language,
                    avatarUrl = user.AvatarUrl
                }
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(ApiResponse.Fail("Refresh token requerido"));

        var user = await _authRepo.GetUserByRefreshTokenAsync(request.RefreshToken);
        if (user is null)
            return Unauthorized(ApiResponse.Fail("Refresh token inválido o expirado"));

        if (!user.IsActive)
            return Unauthorized(ApiResponse.Fail("Cuenta desactivada"));

        // Generate new tokens
        var tokenString = GenerateJwtToken(user);
        var newRefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshExpiresInDays"] ?? "7");
        await _authRepo.SaveRefreshTokenAsync(user.Id, newRefreshToken, DateTime.UtcNow.AddDays(refreshDays));

        return Ok(new
        {
            success = true,
            data = new
            {
                token = tokenString,
                refreshToken = newRefreshToken
            }
        });
    }

    private string GenerateJwtToken(Domain.Entities.User user)
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

    public record RefreshRequest(string RefreshToken);
}
