namespace Walos.Application.Storage;

public sealed class ValidatedImage : IAsyncDisposable
{
    public ValidatedImage(
        Stream content,
        string contentType,
        string extension,
        long length,
        int width,
        int height)
    {
        Content = content;
        ContentType = contentType;
        Extension = extension;
        Length = length;
        Width = width;
        Height = height;
    }

    public Stream Content { get; }
    public string ContentType { get; }
    public string Extension { get; }
    public long Length { get; }
    public int Width { get; }
    public int Height { get; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IImageValidator
{
    Task<ValidatedImage> ValidateAsync(
        Stream content,
        CancellationToken cancellationToken = default);
}
