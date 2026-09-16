using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Walos.Domain.Exceptions;

namespace Walos.Domain.Policies;

public sealed record PosSaleFingerprintItem(long ProductId, decimal Quantity);

public sealed record PosSaleFingerprintPayment(string Method, decimal Amount, string? Reference);

public static class PosSaleIdempotencyPolicy
{
    public const int MaximumKeyLength = 100;

    public static string? NormalizeKey(string? key)
    {
        if (key is null)
            return null;

        var normalized = key.Trim();
        if (normalized.Length == 0 || normalized.Length > MaximumKeyLength)
            throw new ValidationException($"Idempotency-Key debe tener entre 1 y {MaximumKeyLength} caracteres");

        return normalized;
    }

    public static string CreateFingerprint(
        long companyId,
        long branchId,
        IEnumerable<PosSaleFingerprintItem> items,
        IEnumerable<PosSaleFingerprintPayment> payments,
        decimal? cashReceived)
    {
        var canonicalItems = items
            .Select(item => new
            {
                item.ProductId,
                Quantity = SaleItemPolicy.NormalizeQuantity(item.Quantity)
            })
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = SaleItemPolicy.NormalizeQuantity(group.Sum(item => item.Quantity))
            })
            .OrderBy(item => item.ProductId)
            .ToList();

        var canonicalPayments = payments
            .Select(payment => new
            {
                Method = PaymentPolicy.ToAccountingMethod(payment.Method),
                Amount = PaymentPolicy.NormalizePositiveAmount(payment.Amount),
                Reference = NormalizeReference(payment.Reference)
            })
            .GroupBy(payment => new { payment.Method, payment.Reference })
            .Select(group => new
            {
                group.Key.Method,
                group.Key.Reference,
                Amount = PaymentPolicy.RoundMoney(group.Sum(payment => payment.Amount))
            })
            .OrderBy(payment => payment.Method, StringComparer.Ordinal)
            .ThenBy(payment => payment.Reference, StringComparer.Ordinal)
            .ToList();

        var canonical = new StringBuilder()
            .Append("company=").Append(companyId)
            .Append("|branch=").Append(branchId)
            .Append("|items=");

        foreach (var item in canonicalItems)
        {
            canonical
                .Append(item.ProductId)
                .Append(':')
                .Append(FormatQuantity(item.Quantity))
                .Append(';');
        }

        canonical.Append("|payments=");
        foreach (var payment in canonicalPayments)
        {
            canonical
                .Append(payment.Method)
                .Append(':')
                .Append(FormatMoney(payment.Amount))
                .Append(':')
                .Append(payment.Reference is null
                    ? "n"
                    : $"s{Encoding.UTF8.GetByteCount(payment.Reference)}:{payment.Reference}")
                .Append(';');
        }

        canonical
            .Append("|cashReceived=")
            .Append(FormatMoney(PaymentPolicy.RoundMoney(cashReceived ?? 0m)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    public static long CreateAdvisoryLockKey(long companyId, string idempotencyKey)
    {
        var value = Encoding.UTF8.GetBytes($"{companyId}:{idempotencyKey}");
        var hash = SHA256.HashData(value);
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }

    private static string? NormalizeReference(string? reference)
    {
        var normalized = reference?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string FormatMoney(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}
