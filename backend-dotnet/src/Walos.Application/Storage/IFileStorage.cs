namespace Walos.Application.Storage;

public enum ImageStorageScope
{
    Branding,
    Product
}

public sealed record ImageUploadRequest(
    long CompanyId,
    ImageStorageScope Scope,
    long? ProductId,
    Stream Content,
    string? DeclaredFileName = null,
    string? DeclaredContentType = null);

public sealed record StoredFile(
    string ObjectKey,
    string PublicUrl,
    string ContentType,
    long Length,
    int Width,
    int Height);

public interface IFileStorage
{
    Task<StoredFile> UploadImageAsync(
        ImageUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string objectKeyOrManagedPublicUrl,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteIfManagedAsync(
        string objectKeyOrManagedPublicUrl,
        CancellationToken cancellationToken = default);

    bool IsManagedReference(string? objectKeyOrManagedPublicUrl);
    string GetPublicUrl(string objectKey);
}
