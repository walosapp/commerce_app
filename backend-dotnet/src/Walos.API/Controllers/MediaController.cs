using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.Storage;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/media")]
[AllowAnonymous]
public class MediaController : ControllerBase
{
    private readonly IFileStorage _fileStorage;

    public MediaController(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    [HttpGet("image")]
    public IActionResult GetManagedImage([FromQuery] string? key)
    {
        if (!IsCanonicalPublicImageKey(key))
            return BadRequest(new { message = "Referencia de imagen inválida" });

        if (!_fileStorage.IsManagedReference(key))
            return NotFound(new { message = "Imagen no encontrada" });

        var publicUrl = _fileStorage.GetPublicUrl(key!);
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return NotFound(new { message = "Imagen no encontrada" });

        return Redirect(publicUrl);
    }

    private static bool IsCanonicalPublicImageKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains('\\'))
            return false;

        var segments = key.Split('/', StringSplitOptions.None);
        if (segments.Length < 4 ||
            segments[0] != "companies" ||
            !long.TryParse(segments[1], out var companyId) ||
            companyId <= 0)
            return false;

        if (segments.Length == 4 && segments[2] == "branding")
            return IsSafeFileName(segments[3]);

        return segments.Length == 5 &&
               segments[2] == "products" &&
               long.TryParse(segments[3], out var productId) &&
               productId > 0 &&
               IsSafeFileName(segments[4]);
    }

    private static bool IsSafeFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        fileName is not "." and not ".." &&
        fileName.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}
