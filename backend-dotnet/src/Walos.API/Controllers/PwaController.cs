using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Walos.Application.Services;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/pwa")]
[AllowAnonymous]
public class PwaController : ControllerBase
{
    private static readonly int[] AllowedIconSizes = [72, 96, 128, 144, 152, 180, 192, 384, 512];
    private readonly ICompanyService _companyService;
    private readonly IWebHostEnvironment _environment;

    public PwaController(ICompanyService companyService, IWebHostEnvironment environment)
    {
        _companyService = companyService;
        _environment = environment;
    }

    [HttpGet("manifest.webmanifest")]
    public async Task<IActionResult> GetManifest([FromQuery] long tenantId, [FromQuery] string? v = null)
    {
        if (tenantId <= 0)
            return BadRequest(new { message = "tenantId es obligatorio" });

        var settings = await _companyService.GetSettingsAsync(tenantId);
        var displayName = string.IsNullOrWhiteSpace(settings.DisplayName) ? settings.Name : settings.DisplayName;
        var shortName = displayName.Length > 24 ? displayName[..24] : displayName;
        var version = string.IsNullOrWhiteSpace(v) ? settings.LogoUrl ?? "default" : v;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var manifest = new
        {
            id = $"/tenant/{tenantId}/walos",
            name = $"{displayName} | Walos",
            short_name = shortName,
            description = $"Walos para {displayName}",
            theme_color = "#1a73e8",
            background_color = "#ffffff",
            display = "standalone",
            orientation = "portrait",
            scope = "/",
            start_url = "/",
            icons = AllowedIconSizes.Select(size => new
            {
                src = $"{baseUrl}/api/v1/pwa/icon/{tenantId}/{size}.png?v={Uri.EscapeDataString(version)}",
                sizes = $"{size}x{size}",
                type = "image/png",
                purpose = "any maskable"
            })
        };

        return Content(JsonSerializer.Serialize(manifest), "application/manifest+json");
    }

    [HttpGet("icon/{tenantId:long}/{size:int}.png")]
    public async Task<IActionResult> GetIcon(long tenantId, int size)
    {
        if (tenantId <= 0)
            return BadRequest(new { message = "tenantId invalido" });

        if (!AllowedIconSizes.Contains(size))
            return BadRequest(new { message = "Tamano de icono no permitido" });

        var settings = await _companyService.GetSettingsAsync(tenantId);
        var logoPath = ResolveLogoPhysicalPath(settings.LogoUrl);

        if (logoPath is null)
            return NotFound(new { message = "El tenant no tiene logo configurado" });

        await using var sourceStream = System.IO.File.OpenRead(logoPath);
        using var sourceImage = await Image.LoadAsync<Rgba32>(sourceStream);

        sourceImage.Mutate(image => image.AutoOrient());

        var padding = Math.Max((int)Math.Round(size * 0.12), 12);
        var maxLogoSize = Math.Max(size - (padding * 2), 1);

        sourceImage.Mutate(image => image.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxLogoSize, maxLogoSize)
        }));

        using var canvas = new Image<Rgba32>(size, size, Color.White);
        var x = (size - sourceImage.Width) / 2;
        var y = (size - sourceImage.Height) / 2;

        canvas.Mutate(image => image.DrawImage(sourceImage, new Point(x, y), 1f));

        var output = new MemoryStream();
        await canvas.SaveAsPngAsync(output, new PngEncoder());
        output.Position = 0;

        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return File(output, "image/png");
    }

    private string? ResolveLogoPhysicalPath(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullWebRoot = Path.GetFullPath(webRoot);
        var relativePath = logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(fullWebRoot, relativePath));

        if (!candidate.StartsWith(fullWebRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        return System.IO.File.Exists(candidate) ? candidate : null;
    }
}
