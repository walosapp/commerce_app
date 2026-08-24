using System.Buffers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using Walos.Application.Storage;
using Walos.Domain.Exceptions;

namespace Walos.Infrastructure.Storage;

public sealed class ImageSharpImageValidator : IImageValidator
{
    public const long MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxDimension = 4096;
    public const long MaxPixels = 16_777_216;

    public async Task<ValidatedImage> ValidateAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (content is null || !content.CanRead)
            throw new ValidationException("No se proporciono una imagen valida");

        var buffer = new MemoryStream();
        try
        {
            await CopyBoundedAsync(content, buffer, cancellationToken);

            if (buffer.Length == 0)
                throw new ValidationException("No se proporciono una imagen valida");

            buffer.Position = 0;
            var imageInfo = await Image.IdentifyAsync(buffer, cancellationToken);
            ValidateDimensions(imageInfo.Width, imageInfo.Height);

            buffer.Position = 0;
            var format = await Image.DetectFormatAsync(buffer, cancellationToken);
            var (contentType, extension) = ResolveAllowedFormat(format);

            buffer.Position = 0;
            using var decodedImage = await Image.LoadAsync(buffer, cancellationToken);
            if (decodedImage.Frames.Count != 1)
                throw new ValidationException("La imagen debe contener un unico cuadro");

            ValidateDimensions(decodedImage.Width, decodedImage.Height);
            buffer.Position = 0;

            return new ValidatedImage(
                buffer,
                contentType,
                extension,
                buffer.Length,
                decodedImage.Width,
                decodedImage.Height);
        }
        catch (ValidationException)
        {
            await buffer.DisposeAsync();
            throw;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException
                                   or InvalidImageContentException
                                   or NotSupportedException
                                   or ArgumentException)
        {
            await buffer.DisposeAsync();
            throw new ValidationException("El contenido del archivo no es una imagen JPG, PNG o WebP valida");
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    private static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(81_920);
        long totalBytes = 0;

        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(rentedBuffer, cancellationToken)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > MaxFileBytes)
                    throw new ValidationException("La imagen no puede superar 2 MB");

                await destination.WriteAsync(rentedBuffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        var pixels = (long)width * height;
        if (width <= 0 || height <= 0 ||
            width > MaxDimension || height > MaxDimension ||
            pixels > MaxPixels)
        {
            throw new ValidationException("La imagen excede las dimensiones permitidas");
        }
    }

    private static (string ContentType, string Extension) ResolveAllowedFormat(IImageFormat format)
    {
        if (format == JpegFormat.Instance)
            return ("image/jpeg", ".jpg");
        if (format == PngFormat.Instance)
            return ("image/png", ".png");
        if (format == WebpFormat.Instance)
            return ("image/webp", ".webp");

        throw new ValidationException("Formato no permitido. Use JPG, PNG o WebP");
    }
}
