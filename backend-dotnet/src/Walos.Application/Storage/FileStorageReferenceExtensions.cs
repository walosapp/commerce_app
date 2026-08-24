namespace Walos.Application.Storage;

public static class FileStorageReferenceExtensions
{
    public static string? ResolvePublicReference(this IFileStorage storage, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !storage.IsManagedReference(reference))
            return reference;

        return Uri.TryCreate(reference, UriKind.Absolute, out _)
            ? reference
            : storage.GetPublicUrl(reference);
    }
}
