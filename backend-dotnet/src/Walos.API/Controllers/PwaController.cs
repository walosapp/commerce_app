using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Walos.Application.Services;
using Walos.Application.Storage;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/pwa")]
[AllowAnonymous]
public class PwaController : ControllerBase
{
    private static readonly int[] AllowedIconSizes = [72, 96, 128, 144, 152, 180, 192, 384, 512];
    private readonly ICompanyService _companyService;
    private readonly IFileStorage _fileStorage;
    private readonly IWebHostEnvironment _environment;

    public PwaController(
        ICompanyService companyService,
        IFileStorage fileStorage,
        IWebHostEnvironment environment)
    {
        _companyService = companyService;
        _fileStorage = fileStorage;
        _environment = environment;
    }

    [HttpGet("manifest.webmanifest")]
    public async Task<IActionResult> GetManifest([FromQuery] long tenantId, [FromQuery] string? v = null)
    {
        if (tenantId <= 0)
            return BadRequest(new { message = "tenantId es obligatorio" });

        var settings = await _companyService.GetSettingsWithRawLogoAsync(tenantId);
        var displayName = string.IsNullOrWhiteSpace(settings.DisplayName) ? settings.Name : settings.DisplayName;
        var shortName = displayName.Length > 24 ? displayName[..24] : displayName;
        var version = string.IsNullOrWhiteSpace(v) ? settings.LogoUrl ?? "default" : v;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var appOrigin = ResolveAppOrigin(Request.Headers.Origin.FirstOrDefault()) ?? baseUrl;

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
            scope = $"{appOrigin}/",
            start_url = $"{appOrigin}/",
            icons = AllowedIconSizes.Select(size => new
            {
                src = $"/api/v1/pwa/icon/{tenantId}/{size}.png?v={Uri.EscapeDataString(version)}",
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

        var settings = await _companyService.GetSettingsWithRawLogoAsync(tenantId);
        await using var sourceStream = await OpenLogoStreamAsync(settings.LogoUrl, tenantId);

        if (sourceStream is null)
            return NotFound(new { message = "El tenant no tiene logo configurado" });

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

    private async Task<Stream?> OpenLogoStreamAsync(string? logoReference, long tenantId)
    {
        if (string.IsNullOrWhiteSpace(logoReference))
            return null;

        if (IsCanonicalBrandingKey(logoReference, tenantId))
        {
            if (!_fileStorage.IsManagedReference(logoReference))
                return null;

            return await _fileStorage.OpenReadAsync(logoReference, HttpContext.RequestAborted);
        }

        var logoPath = ResolveLegacyLogoPhysicalPath(logoReference);
        return logoPath is null ? null : System.IO.File.OpenRead(logoPath);
    }

    private string? ResolveLegacyLogoPhysicalPath(string logoUrl)
    {
        const string legacyPrefix = "/uploads/branding/";
        if (!logoUrl.StartsWith(legacyPrefix, StringComparison.Ordinal))
            return null;

        var fileName = logoUrl[legacyPrefix.Length..];
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains('/') ||
            fileName.Contains('\\') ||
            fileName is "." or "..")
            return null;

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var brandingRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "branding"));
        var candidate = Path.GetFullPath(Path.Combine(brandingRoot, fileName));

        return System.IO.File.Exists(candidate) ? candidate : null;
    }

    private static bool IsCanonicalBrandingKey(string reference, long tenantId)
    {
        var prefix = $"companies/{tenantId}/branding/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var fileName = reference[prefix.Length..];
        return fileName.Length > 0 &&
               !fileName.Contains('/') &&
               !fileName.Contains('\\') &&
               fileName is not "." and not "..";
    }

    private static string? ResolveAppOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
