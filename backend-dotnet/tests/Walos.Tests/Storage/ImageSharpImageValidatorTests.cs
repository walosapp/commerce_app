using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Walos.Domain.Exceptions;
using Walos.Infrastructure.Storage;

namespace Walos.Tests.Storage;

public class ImageSharpImageValidatorTests
{
    private readonly ImageSharpImageValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_UsesDecodedPngMetadata()
    {
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(3, 2))
            await image.SaveAsPngAsync(input);
        input.Position = 0;

        await using var result = await _validator.ValidateAsync(input);

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(".png", result.Extension);
        Assert.Equal(3, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(result.Length, result.Content.Length);
        Assert.Equal(0, result.Content.Position);
    }

    [Fact]
    public async Task ValidateAsync_RejectsDeclaredImageBytesThatAreNotAnImage()
    {
        await using var input = new MemoryStream("not-an-image"u8.ToArray());

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAsync(input));
    }

    [Fact]
    public async Task ValidateAsync_RejectsContentLargerThanTwoMegabytes()
    {
        await using var input = new MemoryStream(
            new byte[checked((int)ImageSharpImageValidator.MaxFileBytes + 1)]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAsync(input));

        Assert.Contains("2 MB", exception.Message);
    }

    [Fact]
    public async Task ValidateAsync_RejectsDimensionsLargerThanLimit()
    {
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(ImageSharpImageValidator.MaxDimension + 1, 1))
            await image.SaveAsPngAsync(input);
        input.Position = 0;

        await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAsync(input));
    }
}
