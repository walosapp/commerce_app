using Walos.Domain.Exceptions;
using Walos.Domain.Policies;

namespace Walos.Tests.Policies;

public class PosSaleIdempotencyPolicyTests
{
    [Fact]
    public void Fingerprint_Is_Stable_When_Item_And_Payment_Order_Changes()
    {
        var first = Fingerprint(
            [new(10, 2m), new(20, 1m)],
            [new("cash", 10m, null), new("card", 20m, "AUTH")]);
        var reordered = Fingerprint(
            [new(20, 1m), new(10, 2m)],
            [new("card", 20m, "AUTH"), new("cash", 10m, null)]);

        Assert.Equal(first, reordered);
    }

    [Fact]
    public void Fingerprint_Groups_Repeated_Products_Deterministically()
    {
        var repeated = Fingerprint(
            [new(10, 0.25m), new(10, 0.75m)],
            [new("cash", 30m, null)]);
        var aggregated = Fingerprint(
            [new(10, 1m)],
            [new("cash", 30m, null)]);

        Assert.Equal(repeated, aggregated);
    }

    [Fact]
    public void Fingerprint_Treats_Legacy_Nequi_As_Transfer()
    {
        var nequi = Fingerprint([new(10, 1m)], [new("nequi", 30m, "REF")]);
        var transfer = Fingerprint([new(10, 1m)], [new("transfer", 30m, "REF")]);

        Assert.Equal(nequi, transfer);
    }

    [Theory]
    [InlineData(2, 10, "cash", 30, 0)]
    [InlineData(1, 11, "cash", 30, 0)]
    [InlineData(1, 10, "card", 30, 0)]
    [InlineData(1, 10, "cash", 31, 0)]
    [InlineData(1, 10, "cash", 30, 50)]
    public void Fingerprint_Changes_When_Economic_Intent_Changes(
        int quantity,
        long productId,
        string method,
        int amount,
        int cashReceived)
    {
        var baseline = Fingerprint([new(10, 1m)], [new("cash", 30m, null)]);
        var changed = Fingerprint(
            [new(productId, quantity)],
            [new(method, amount, null)],
            cashReceived);

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void Fingerprint_Does_Not_Accept_Unsupported_Quantity_Precision()
    {
        Assert.Throws<ValidationException>(() => Fingerprint(
            [new(10, 1.255m)],
            [new("cash", 30m, null)]));
    }

    [Fact]
    public void Advisory_Lock_Key_Is_Stable_And_Tenant_Scoped()
    {
        var first = PosSaleIdempotencyPolicy.CreateAdvisoryLockKey(1, "same-key");

        Assert.Equal(first, PosSaleIdempotencyPolicy.CreateAdvisoryLockKey(1, "same-key"));
        Assert.NotEqual(first, PosSaleIdempotencyPolicy.CreateAdvisoryLockKey(2, "same-key"));
    }

    [Fact]
    public void NormalizeKey_Rejects_Blank_Or_Overlong_Keys()
    {
        Assert.Throws<ValidationException>(() => PosSaleIdempotencyPolicy.NormalizeKey("  "));
        Assert.Throws<ValidationException>(() => PosSaleIdempotencyPolicy.NormalizeKey(new string('x', 101)));
        Assert.Equal("key", PosSaleIdempotencyPolicy.NormalizeKey(" key "));
        Assert.Null(PosSaleIdempotencyPolicy.NormalizeKey(null));
    }

    private static string Fingerprint(
        IEnumerable<PosSaleFingerprintItem> items,
        IEnumerable<PosSaleFingerprintPayment> payments,
        decimal? cashReceived = null) =>
        PosSaleIdempotencyPolicy.CreateFingerprint(1, 2, items, payments, cashReceived);
}
