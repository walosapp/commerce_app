using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Walos.Infrastructure.Services;

public static class AesEncryptionHelper
{
    private static string? _keyHex;

    public static void Configure(IConfiguration configuration)
    {
        _keyHex = configuration["Security:AiKeyEncryptionSecret"];
    }

    public static string Encrypt(string plainText)
    {
        var key = GetKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherText)
    {
        var key = GetKey();
        var allBytes = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[aes.BlockSize / 8];
        var cipher = new byte[allBytes.Length - iv.Length];
        Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(allBytes, iv.Length, cipher, 0, cipher.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] GetKey()
    {
        if (string.IsNullOrWhiteSpace(_keyHex))
            throw new InvalidOperationException("AI_KEY_ENCRYPTION_SECRET no configurado.");

        return Convert.FromHexString(_keyHex.PadRight(64, '0')[..64]);
    }
}
