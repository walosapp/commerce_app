using Walos.PrintAgent.Api;

namespace Walos.PrintAgent.Tests;

public sealed class ReceiptFingerprintTests
{
    [Fact]
    public void SameDocument_ProducesStableFingerprint()
    {
        var request = ReceiptTestData.CreateRequest();

        var first = ReceiptFingerprint.Compute(request);
        var second = ReceiptFingerprint.Compute(request with { JobId = "another-job", Fingerprint = "ignored" });

        Assert.Equal(first, second);
        Assert.Matches("^[a-f0-9]{64}$", first);
    }

    [Fact]
    public void UnicodeCanonicalEquivalents_ProduceSameFingerprint()
    {
        var request = ReceiptTestData.CreateRequest();
        var composed = request with
        {
            Receipt = request.Receipt with { CompanyName = "Café" }
        };
        var decomposed = request with
        {
            Receipt = request.Receipt with { CompanyName = "Cafe\u0301" }
        };

        Assert.Equal(ReceiptFingerprint.Compute(composed), ReceiptFingerprint.Compute(decomposed));
    }

    [Fact]
    public void CanonicalFingerprint_MatchesJavaScriptCompatibilityVector()
    {
        var fingerprint = ReceiptFingerprint.Compute(ReceiptTestData.CreateRequest());

        Assert.Equal("39d63b78c43df93f77e2a0500647e76fb829acfb28a504425a2a73a9ba928d02", fingerprint);
    }

    [Fact]
    public void CanonicalFingerprint_MatchesFrontendGoldenVector()
    {
        var request = new PrintReceiptRequest(
            1,
            "ignored-job",
            25,
            7,
            123,
            new ReceiptDocument(
                "Comercio",
                null,
                null,
                null,
                null,
                "COP",
                "America/Bogota",
                123,
                "ORD-123",
                "completed",
                null,
                "Mostrador",
                1,
                "2026-09-14T17:30:00.000Z",
                "Maria",
                [new ReceiptItemDocument("Cafe", 1m, 5000m, 5000m)],
                5000m,
                null,
                0m,
                0m,
                5000m,
                0m,
                false,
                1,
                [new ReceiptPaymentDocument("cash", 5000m, null)],
                false,
                null,
                null,
                null,
                null,
                null),
            string.Empty);

        Assert.Equal(
            "884c19fd826b0ed7c5b9f38e7e82d5196e6a74925e5e78c77a39d3969acffcac",
            ReceiptFingerprint.Compute(request));
    }

    [Fact]
    public void ChangedTotal_ProducesDifferentFingerprint()
    {
        var request = ReceiptTestData.CreateRequest();
        var changed = request with
        {
            Receipt = request.Receipt with { FinalTotalPaid = request.Receipt.FinalTotalPaid + 1m }
        };

        Assert.NotEqual(ReceiptFingerprint.Compute(request), ReceiptFingerprint.Compute(changed));
    }

    [Fact]
    public void ChangedItem_ProducesDifferentFingerprint()
    {
        var request = ReceiptTestData.CreateRequest();
        var changedItems = request.Receipt.Items.ToArray();
        changedItems[0] = changedItems[0] with { Quantity = 3m };
        var changed = request with { Receipt = request.Receipt with { Items = changedItems } };

        Assert.NotEqual(ReceiptFingerprint.Compute(request), ReceiptFingerprint.Compute(changed));
    }
}
