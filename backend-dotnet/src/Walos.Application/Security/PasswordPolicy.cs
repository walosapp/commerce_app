using Walos.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace Walos.Application.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    private const string InvalidAsciiPattern = @"[^\x21-\x7E]";
    private const string UppercasePattern = @"[A-Z]";
    private const string LowercasePattern = @"[a-z]";
    private const string DigitPattern = @"[0-9]";
    private const string SpecialPattern = @"[^A-Za-z0-9]";

    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
            throw new ValidationException("La contraseña debe tener al menos 8 caracteres");

        if (Regex.IsMatch(password, InvalidAsciiPattern, RegexOptions.CultureInvariant)
            || !Regex.IsMatch(password, UppercasePattern, RegexOptions.CultureInvariant)
            || !Regex.IsMatch(password, LowercasePattern, RegexOptions.CultureInvariant)
            || !Regex.IsMatch(password, DigitPattern, RegexOptions.CultureInvariant)
            || !Regex.IsMatch(password, SpecialPattern, RegexOptions.CultureInvariant))
        {
            throw new ValidationException(
                "La contraseña debe usar caracteres ASCII visibles e incluir mayúscula, minúscula, número y carácter especial, sin espacios");
        }
    }
}
