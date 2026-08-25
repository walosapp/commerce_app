using System.Collections.Frozen;
using Walos.Domain.Exceptions;

namespace Walos.Domain.Policies;

public static class PaymentPolicy
{
    public const int MoneyDecimals = 2;
    public const decimal ReconciliationTolerance = 0.01m;

    public static IReadOnlySet<string> CanonicalMethods { get; } =
        new[] { "cash", "card", "transfer", "other" }.ToFrozenSet(StringComparer.Ordinal);

    // nequi remains accepted and persisted during the compatibility window.
    public static IReadOnlySet<string> AcceptedMethods { get; } =
        new[] { "cash", "card", "transfer", "other", "nequi" }.ToFrozenSet(StringComparer.Ordinal);

    public static string NormalizeMethod(string? method)
    {
        var normalized = method?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AcceptedMethods.Contains(normalized))
            throw new ValidationException("Metodo de pago invalido");

        return normalized;
    }

    public static bool IsAcceptedMethod(string? method)
    {
        var normalized = method?.Trim().ToLowerInvariant();
        return normalized is not null && AcceptedMethods.Contains(normalized);
    }

    public static string ToAccountingMethod(string method)
    {
        var normalized = NormalizeMethod(method);
        return normalized == "nequi" ? "transfer" : normalized;
    }

    public static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, MoneyDecimals, MidpointRounding.AwayFromZero);

    public static decimal NormalizePositiveAmount(decimal amount)
    {
        var normalized = RoundMoney(amount);
        if (normalized <= 0)
            throw new ValidationException("Todos los pagos deben tener un valor mayor que cero");

        return normalized;
    }

    public static void ValidatePaymentTotal(decimal expectedAmount, IEnumerable<decimal> paymentAmounts)
    {
        var expected = RoundMoney(expectedAmount);
        var actual = RoundMoney(paymentAmounts.Sum(RoundMoney));
        if (Math.Abs(actual - expected) > ReconciliationTolerance)
            throw new ValidationException("La suma de los pagos no coincide con el total a cobrar");
    }

    public static string? ResolvePersistedMethod(IEnumerable<string> methods)
    {
        var normalized = methods
            .Select(NormalizeMethod)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return normalized.Count switch
        {
            0 => null,
            1 => normalized[0],
            _ => "mixed"
        };
    }
}
