using System.Text;

namespace Walos.API.Security;

public static class JwtSecretValidator
{
    private const int MinimumBytes = 32;

    private static readonly string[] ForbiddenFragments =
    [
        "change-this",
        "your-super-secret",
        "replace-me",
        "development-secret",
        "default-secret"
    ];

    public static void ValidateOrThrow(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw Invalid();

        var normalized = secret.Trim();
        if (Encoding.UTF8.GetByteCount(normalized) < MinimumBytes)
            throw Invalid();

        if (ForbiddenFragments.Any(fragment =>
                normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            throw Invalid();

        var characterClasses = 0;
        if (normalized.Any(char.IsLower)) characterClasses++;
        if (normalized.Any(char.IsUpper)) characterClasses++;
        if (normalized.Any(char.IsDigit)) characterClasses++;
        if (normalized.Any(character => !char.IsLetterOrDigit(character))) characterClasses++;

        if (characterClasses < 3 || normalized.Distinct().Count() < 12)
            throw Invalid();
    }

    private static InvalidOperationException Invalid() => new(
        "Jwt:Secret debe ser un secreto no predeterminado de al menos 32 bytes y con suficiente diversidad.");
}
