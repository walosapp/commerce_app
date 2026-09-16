using System.Security.Cryptography;
using System.Text;
using Walos.Domain.Entities;

namespace Walos.Application.Security;

public static class AccessTokenSecurityStamp
{
    public static string Compute(string secret, User user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(user);

        var securityState = $"{user.Id}|{user.CompanyId}|{user.BranchId?.ToString() ?? string.Empty}|"
            + $"{user.RoleCode ?? string.Empty}|{user.CompanyTaxId ?? string.Empty}|{user.PasswordHash}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(securityState)))
            .ToLowerInvariant();
    }

    public static bool FixedTimeEquals(string expected, string presented)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(presented))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(presented));
    }
}
