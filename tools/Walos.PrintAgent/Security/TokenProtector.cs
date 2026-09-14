using System.Security.Cryptography;
using System.Text;

namespace Walos.PrintAgent.Security;

public interface ITokenProtector
{
    string Protect(string token);
    string Unprotect(string protectedToken);
}

public sealed class DpapiTokenProtector : ITokenProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Walos.PrintAgent.v1");

    public string Protect(string token)
    {
        var plaintext = Encoding.UTF8.GetBytes(token);
        var protectedBytes = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(plaintext);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedToken)
    {
        var protectedBytes = Convert.FromBase64String(protectedToken);
        var plaintext = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
