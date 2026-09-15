using Walos.API.Security;
using System.Runtime.CompilerServices;

namespace Walos.Tests.Security;

public sealed class JwtSecretValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short-secret")]
    [InlineData("your-super-secret-jwt-key-change-this-in-production-must-be-at-least-32-chars")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ValidateOrThrow_RejectsMissingWeakOrKnownPlaceholderSecrets(string? secret)
    {
        Assert.Throws<InvalidOperationException>(() => JwtSecretValidator.ValidateOrThrow(secret));
    }

    [Fact]
    public void ValidateOrThrow_AcceptsLongNonPlaceholderSecretWithMixedCharacterClasses()
    {
        JwtSecretValidator.ValidateOrThrow("Walos-Test_Only-2026-Secret-With-Enough-Entropy!");
    }

    [Fact]
    public void Program_ValidatesConfiguredJwtSecretBeforeRegisteringAuthentication()
    {
        var backendRoot = GetBackendRoot();
        var program = File.ReadAllText(Path.Combine(backendRoot, "src", "Walos.API", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(backendRoot, "src", "Walos.API", "appsettings.json"));

        Assert.Contains("JwtSecretValidator.ValidateOrThrow(jwtSecret);", program);
        Assert.DoesNotContain("your-super-secret-jwt-key", appsettings, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBackendRoot([CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException("No se pudo resolver el directorio de tests.");
        return Path.GetFullPath(Path.Combine(testSourceDirectory, "..", "..", ".."));
    }
}
