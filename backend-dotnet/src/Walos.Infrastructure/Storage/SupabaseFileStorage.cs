using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Walos.Application.Storage;
using Walos.Domain.Exceptions;

namespace Walos.Infrastructure.Storage;

public sealed partial class SupabaseFileStorage : IFileStorage
{
    private const string ImmutableCacheControl = "31536000";

    private readonly HttpClient _httpClient;
    private readonly IImageValidator _imageValidator;
    private readonly ILogger<SupabaseFileStorage> _logger;
    private readonly string _serviceRoleKey;
    private readonly string _bucket;
    private readonly Uri _storageApiBaseUri;
    private readonly Uri _publicBaseUri;

    public SupabaseFileStorage(
        HttpClient httpClient,
        IImageValidator imageValidator,
        IOptions<SupabaseStorageOptions> options,
        ILogger<SupabaseFileStorage> logger)
    {
        _httpClient = httpClient;
        _imageValidator = imageValidator;
        _logger = logger;

        var settings = options.Value;
        _serviceRoleKey = settings.ServiceRoleKey;
        _bucket = settings.Bucket;

        var projectBaseUri = new Uri($"{settings.Url.TrimEnd('/')}/", UriKind.Absolute);
        _storageApiBaseUri = new Uri(projectBaseUri, "storage/v1/");
        _publicBaseUri = new Uri(
            _storageApiBaseUri,
            $"object/public/{Uri.EscapeDataString(_bucket)}/");
    }

    public async Task<StoredFile> UploadImageAsync(
        ImageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadRequest(request);

        await using var validatedImage = await _imageValidator.ValidateAsync(
            request.Content,
            cancellationToken);

        var objectKey = BuildObjectKey(request, validatedImage.Extension);
        var requestUri = BuildStorageUri("object", _bucket, objectKey);

        using var uploadRequest = CreateAuthorizedRequest(HttpMethod.Post, requestUri);
        uploadRequest.Headers.TryAddWithoutValidation("x-upsert", "false");
        uploadRequest.Headers.TryAddWithoutValidation("cache-control", ImmutableCacheControl);

        var streamContent = new StreamContent(validatedImage.Content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(validatedImage.ContentType);
        uploadRequest.Content = streamContent;

        using var response = await _httpClient.SendAsync(
            uploadRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase Storage rechazo upload de {ObjectKey} con HTTP {StatusCode}",
                objectKey,
                (int)response.StatusCode);
            throw new InvalidOperationException("No fue posible almacenar la imagen");
        }

        return new StoredFile(
            objectKey,
            GetPublicUrl(objectKey),
            validatedImage.ContentType,
            validatedImage.Length,
            validatedImage.Width,
            validatedImage.Height);
    }

    public async Task<Stream?> OpenReadAsync(
        string objectKeyOrManagedPublicUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveObjectKey(objectKeyOrManagedPublicUrl, out var objectKey))
            throw new ValidationException("La referencia del archivo no pertenece al storage configurado");

        var requestUri = BuildStorageUri("object", _bucket, objectKey);
        using var request = CreateAuthorizedRequest(HttpMethod.Get, requestUri);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase Storage rechazo lectura de {ObjectKey} con HTTP {StatusCode}",
                objectKey,
                (int)response.StatusCode);
            throw new InvalidOperationException("No fue posible leer la imagen almacenada");
        }

        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task<bool> DeleteIfManagedAsync(
        string objectKeyOrManagedPublicUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveObjectKey(objectKeyOrManagedPublicUrl, out var objectKey))
            return false;

        var requestUri = BuildStorageUri("object", _bucket);
        using var request = CreateAuthorizedRequest(HttpMethod.Delete, requestUri);
        request.Content = JsonContent.Create(new { prefixes = new[] { objectKey } });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Supabase Storage rechazo delete de {ObjectKey} con HTTP {StatusCode}",
                objectKey,
                (int)response.StatusCode);
            throw new InvalidOperationException("No fue posible eliminar la imagen almacenada");
        }

        return true;
    }

    public bool IsManagedReference(string? objectKeyOrManagedPublicUrl) =>
        TryResolveObjectKey(objectKeyOrManagedPublicUrl, out _);

    public bool TryGetManagedObjectKey(
        string? objectKeyOrManagedPublicUrl,
        out string objectKey) =>
        TryResolveObjectKey(objectKeyOrManagedPublicUrl, out objectKey);

    public string GetPublicUrl(string objectKey)
    {
        if (!IsCanonicalObjectKey(objectKey))
            throw new ValidationException("La clave del archivo no es valida");

        return new Uri(_publicBaseUri, objectKey).AbsoluteUri;
    }

    private static void ValidateUploadRequest(ImageUploadRequest request)
    {
        if (request.CompanyId <= 0)
            throw new ValidationException("Empresa invalida para almacenar la imagen");
        if (request.Content is null || !request.Content.CanRead)
            throw new ValidationException("No se proporciono una imagen valida");

        if (request.Scope == ImageStorageScope.Product && request.ProductId is not > 0)
            throw new ValidationException("Producto invalido para almacenar la imagen");
        if (request.Scope == ImageStorageScope.Branding && request.ProductId.HasValue)
            throw new ValidationException("El branding no puede asociarse a un producto");
        if (!Enum.IsDefined(request.Scope))
            throw new ValidationException("Destino de imagen no permitido");
    }

    private static string BuildObjectKey(ImageUploadRequest request, string extension)
    {
        var immutableName = Guid.NewGuid().ToString("N");
        return request.Scope switch
        {
            ImageStorageScope.Branding =>
                $"companies/{request.CompanyId}/branding/logo-{immutableName}{extension}",
            ImageStorageScope.Product =>
                $"companies/{request.CompanyId}/products/{request.ProductId!.Value}/image-{immutableName}{extension}",
            _ => throw new ValidationException("Destino de imagen no permitido")
        };
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, Uri requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        request.Headers.TryAddWithoutValidation("apikey", _serviceRoleKey);
        return request;
    }

    private Uri BuildStorageUri(params string[] segments)
    {
        var escapedPath = string.Join('/', segments.Select(EscapePath));
        return new Uri(_storageApiBaseUri, escapedPath);
    }

    private static string EscapePath(string value) =>
        string.Join('/', value.Split('/').Select(Uri.EscapeDataString));

    private bool TryResolveObjectKey(string? reference, out string objectKey)
    {
        objectKey = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        if (IsCanonicalObjectKey(reference))
        {
            objectKey = reference;
            return true;
        }

        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.Equals(uri.Scheme, _publicBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, _publicBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != _publicBaseUri.Port)
        {
            return false;
        }

        var publicPathPrefix = _publicBaseUri.AbsolutePath;
        if (!uri.AbsolutePath.StartsWith(publicPathPrefix, StringComparison.Ordinal))
            return false;

        var encodedObjectKey = uri.AbsolutePath[publicPathPrefix.Length..];
        objectKey = Uri.UnescapeDataString(encodedObjectKey);
        if (!IsCanonicalObjectKey(objectKey))
        {
            objectKey = string.Empty;
            return false;
        }

        return true;
    }

    private static bool IsCanonicalObjectKey(string objectKey) =>
        CanonicalObjectKeyRegex().IsMatch(objectKey);

    [GeneratedRegex(
        @"^companies/[1-9]\d*/(?:branding/logo-[a-f0-9]{32}|products/[1-9]\d*/image-[a-f0-9]{32})\.(?:jpg|png|webp)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalObjectKeyRegex();
}
