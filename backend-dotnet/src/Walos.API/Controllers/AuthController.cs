using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public record LoginRequest(string Username, string Password);

    public record LoginResponse(string Token, object User);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Dev credentials
        if (request.Username != "dev" || request.Password != "1234")
        {
            _logger.LogWarning("Intento de login fallido para usuario: {Username}", request.Username);
            return Unauthorized(new { success = false, message = "Credenciales inválidas" });
        }

        var jwtSecret = _configuration["Jwt:Secret"]!;
        var expiresInMinutes = int.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim("userId", "1"),
            new Claim("companyId", "1"),
            new Claim("branchId", "1"),
            new Claim(ClaimTypes.Name, "dev"),
            new Claim(ClaimTypes.Email, "admin@mibar.com"),
            new Claim(ClaimTypes.Role, "admin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("Login exitoso para usuario: {Username}", request.Username);

        return Ok(new
        {
            success = true,
            data = new
            {
                token = tokenString,
                user = new
                {
                    id = 1,
                    name = "Dev User",
                    email = "admin@mibar.com",
                    role = "admin",
                    companyId = 1,
                    branchId = 1
                }
            }
        });
    }
}
