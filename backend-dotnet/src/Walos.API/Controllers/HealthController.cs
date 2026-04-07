using Microsoft.AspNetCore.Mvc;

namespace Walos.API.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public HealthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow.ToString("o"),
            uptime = Environment.TickCount64 / 1000.0,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        });
    }

    /// <summary>
    /// API info endpoint
    /// </summary>
    [HttpGet("api/v1")]
    public IActionResult ApiInfo()
    {
        return Ok(new
        {
            name = "Walos API",
            version = "v1",
            description = "API para gestión comercial con asistencia de IA",
            endpoints = new
            {
                inventory = "/api/v1/inventory",
                sales = "/api/v1/sales",
                suppliers = "/api/v1/suppliers",
                users = "/api/v1/users"
            }
        });
    }
}
