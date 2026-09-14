using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Walos.PrintAgent.Api;
using Walos.PrintAgent.Printing;
using Walos.PrintAgent.Security;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Tests;

public sealed class PrintReceiptApiTests : IAsyncLifetime
{
    private const string Token = "receipt-test-token";
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "walos-print-agent-tests",
        Guid.NewGuid().ToString("N"));
    private readonly RecordingRawPrinter _rawPrinter = new();
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["WALOS_PRINT_AGENT_ALLOWED_ORIGINS"] = "https://walos.test";
        PrintAgentApi.ConfigureServices(builder.Services, builder.Configuration, _directory);
        builder.Services.RemoveAll<ITokenProtector>();
        builder.Services.AddSingleton<ITokenProtector, PlaintextTestTokenProtector>();
        builder.Services.RemoveAll<IPrinterCatalog>();
        builder.Services.AddSingleton<IPrinterCatalog>(new InstalledPrinterCatalog("POS-58"));
        builder.Services.RemoveAll<IRawPrinter>();
        builder.Services.AddSingleton<IRawPrinter>(_rawPrinter);

        _application = builder.Build();
        PrintAgentApi.ConfigurePipeline(_application);
        await _application.StartAsync();

        var store = _application.Services.GetRequiredService<AgentStateStore>();
        await store.SetPairingAsync(
            Token,
            new WorkstationIdentity(10, 20, "POS-1"),
            CancellationToken.None);
        await store.SetPrinterConfigurationAsync(
            new PrinterConfiguration(10, 20, "POS-1", "POS-58", 0, 50, 200),
            CancellationToken.None);
        _client = _application.GetTestClient();
    }

    [Fact]
    public async Task SameJobAndDocument_ReplaysWithoutSecondSpool()
    {
        var request = ReceiptTestData.CreateRequest();

        using var first = await SendAsync(request);
        using var second = await SendAsync(request);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("completed", firstBody.GetProperty("status").GetString());
        Assert.True(firstBody.GetProperty("executed").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("replayed", secondBody.GetProperty("status").GetString());
        Assert.False(secondBody.GetProperty("executed").GetBoolean());
        Assert.Equal(1, _rawPrinter.CallCount);
        Assert.False(Contains(_rawPrinter.LastData, [0x1B, 0x70]));
    }

    [Fact]
    public async Task SameJobWithDifferentDocument_ReturnsConflictWithoutSecondSpool()
    {
        var request = ReceiptTestData.CreateRequest("conflict-job");
        using var first = await SendAsync(request);
        var changed = request with
        {
            Receipt = request.Receipt with { CompanyAddress = "Carrera 99 # 1-2" }
        };
        changed = changed with { Fingerprint = ReceiptFingerprint.Compute(changed) };

        using var second = await SendAsync(changed);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("job_id_conflict", body.GetProperty("code").GetString());
        Assert.Equal(1, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task ClientFingerprintIsRecomputedAndMismatchIsRejectedBeforeSpooling()
    {
        var request = ReceiptTestData.CreateRequest("bad-fingerprint") with
        {
            Fingerprint = new string('0', 64)
        };

        using var response = await SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("fingerprint_mismatch", body.GetProperty("code").GetString());
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task ReceiptFromAnotherPairedContext_IsRejectedBeforeSpooling()
    {
        var request = ReceiptTestData.CreateRequest("other-company") with { CompanyId = 99 };
        request = request with { Fingerprint = ReceiptFingerprint.Compute(request) };

        using var response = await SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task ExcessiveReceiptPayload_IsRejected()
    {
        using var message = CreateMessage(new StringContent(
            JsonSerializer.Serialize(new { payload = new string('x', (int)PrintAgentApi.PrintReceiptMaxPayloadBytes + 1) }),
            Encoding.UTF8,
            "application/json"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task HtmlAndControlCommandsAreRejected()
    {
        var html = ReceiptTestData.CreateRequest("html") with
        {
            Receipt = ReceiptTestData.CreateDocument() with { CompanyName = "<b>Comercio</b>" }
        };
        html = html with { Fingerprint = ReceiptFingerprint.Compute(html) };
        var control = ReceiptTestData.CreateRequest("esc") with
        {
            Receipt = ReceiptTestData.CreateDocument() with { CompanyName = "Comercio\u001Bp" }
        };
        control = control with { Fingerprint = ReceiptFingerprint.Compute(control) };

        using var htmlResponse = await SendAsync(html);
        using var controlResponse = await SendAsync(control);

        Assert.Equal(HttpStatusCode.BadRequest, htmlResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, controlResponse.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task UnknownFieldsAreRejectedBeforeSpooling()
    {
        var json = JsonSerializer.SerializeToNode(ReceiptTestData.CreateRequest("unknown-field"))!.AsObject();
        json["html"] = "<b>not accepted</b>";
        using var message = CreateMessage(new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json"));

        using var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task InvalidOriginAndInvalidTokenAreRejectedBeforeSpooling()
    {
        var request = ReceiptTestData.CreateRequest("security");
        using var badOrigin = CreateMessage(JsonContent.Create(request));
        badOrigin.Headers.Remove("Origin");
        badOrigin.Headers.Add("Origin", "https://attacker.test");
        using var originResponse = await _client.SendAsync(badOrigin);

        using var badToken = CreateMessage(JsonContent.Create(request));
        badToken.Headers.Authorization = new("Bearer", "invalid-token");
        using var tokenResponse = await _client.SendAsync(badToken);

        Assert.Equal(HttpStatusCode.Forbidden, originResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tokenResponse.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task InconsistentPersistedTotalsAreRejectedBeforeSpooling()
    {
        var request = ReceiptTestData.CreateRequest("bad-totals");
        request = request with
        {
            Receipt = request.Receipt with { FinalTotalPaid = request.Receipt.FinalTotalPaid + 1m }
        };
        request = request with { Fingerprint = ReceiptFingerprint.Compute(request) };

        using var response = await SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task PartialAndPaidCreditBalancesAreAcceptedAsPersistedState()
    {
        var partial = ReceiptTestData.CreateRequest("partial-credit");
        partial = partial with { Receipt = partial.Receipt with
        {
            CreditStatus = "partial",
            CreditAmountPaid = 16_150.90m,
            CreditAmount = 500m
        } };
        partial = partial with { Fingerprint = ReceiptFingerprint.Compute(partial) };
        var paid = ReceiptTestData.CreateRequest("paid-credit");
        paid = paid with { Receipt = paid.Receipt with
        {
            CreditStatus = "paid",
            CreditAmountPaid = 16_650.90m,
            CreditAmount = 0m
        } };
        paid = paid with { Fingerprint = ReceiptFingerprint.Compute(paid) };

        using var partialResponse = await SendAsync(partial);
        using var paidResponse = await SendAsync(paid);

        Assert.Equal(HttpStatusCode.OK, partialResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, paidResponse.StatusCode);
        Assert.Equal(2, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task CancelledCredit_PreservesAccountingButPrintsAsNotCurrent()
    {
        var request = ReceiptTestData.CreateRequest("cancelled-credit");
        request = request with
        {
            Receipt = request.Receipt with { CreditStatus = "cancelled" }
        };
        request = request with { Fingerprint = ReceiptFingerprint.Compute(request) };

        using var response = await SendAsync(request);
        var printed = Encoding.GetEncoding(858).GetString(_rawPrinter.LastData);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CRÉDITO CANCELADO", printed);
        Assert.Contains("NO VIGENTE / NO EXIGIBLE", printed);
        Assert.Equal(1, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task HistoricalCanonicalPaymentFallback_IsAcceptedWithoutInventedReference()
    {
        var request = ReceiptTestData.CreateRequest("legacy-payment");
        request = request with
        {
            Receipt = request.Receipt with
            {
                Payments = [new ReceiptPaymentDocument("cash", 16_650.90m, null)]
            }
        };
        request = request with { Fingerprint = ReceiptFingerprint.Compute(request) };

        using var response = await SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _rawPrinter.CallCount);
        Assert.False(Contains(_rawPrinter.LastData, [0x1B, 0x70]));
    }

    [Fact]
    public async Task CancelledOrRefundedReceiptIsRejectedBeforeSpooling()
    {
        var cancelled = ReceiptTestData.CreateRequest("cancelled");
        cancelled = cancelled with { Receipt = cancelled.Receipt with { Status = "cancelled" } };
        cancelled = cancelled with { Fingerprint = ReceiptFingerprint.Compute(cancelled) };
        var refunded = ReceiptTestData.CreateRequest("refunded");
        refunded = refunded with { Receipt = refunded.Receipt with { RefundStatus = "full_refund" } };
        refunded = refunded with { Fingerprint = ReceiptFingerprint.Compute(refunded) };

        using var cancelledResponse = await SendAsync(cancelled);
        using var refundedResponse = await SendAsync(refunded);

        Assert.Equal(HttpStatusCode.BadRequest, cancelledResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refundedResponse.StatusCode);
        Assert.Equal(0, _rawPrinter.CallCount);
    }

    [Fact]
    public async Task WorstCaseUtf8ReceiptWithinSchemaAndTransportLimitIsAccepted()
    {
        var items = Enumerable.Range(1, 100)
            .Select(_ => new ReceiptItemDocument(
                new string('ñ', 200),
                1m,
                1m,
                1m))
            .ToArray();
        var request = ReceiptTestData.CreateRequest("large-valid");
        request = request with
        {
            Receipt = request.Receipt with
            {
                Items = items,
                Subtotal = 100m,
                DiscountType = null,
                DiscountValue = 0m,
                DiscountAmount = 0m,
                FinalTotalPaid = 100m,
                TipAmount = 0m,
                TipIncluded = false,
                Payments = [new ReceiptPaymentDocument("cash", 100m, null)],
                HasCredit = false,
                CreditStatus = null,
                CreditOriginalTotal = null,
                CreditAmountPaid = null,
                CreditAmount = null,
                CreditCustomerName = null
            }
        };
        request = request with { Fingerprint = ReceiptFingerprint.Compute(request) };
        var content = JsonContent.Create(request);
        var serializedLength = (await content.ReadAsByteArrayAsync()).Length;

        using var response = await _client.SendAsync(CreateMessage(content));

        Assert.InRange(serializedLength, (32 * 1024) + 1, PrintAgentApi.PrintReceiptMaxPayloadBytes);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _rawPrinter.CallCount);
    }

    private Task<HttpResponseMessage> SendAsync(PrintReceiptRequest request) =>
        _client.SendAsync(CreateMessage(JsonContent.Create(request)));

    private static HttpRequestMessage CreateMessage(HttpContent content)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/commands/print-receipt");
        message.Headers.Add("Origin", "https://walos.test");
        message.Headers.Authorization = new("Bearer", Token);
        message.Content = content;
        return message;
    }

    private static bool Contains(byte[] source, byte[] expected)
    {
        for (var index = 0; index <= source.Length - expected.Length; index++)
        {
            if (source.AsSpan(index, expected.Length).SequenceEqual(expected))
            {
                return true;
            }
        }

        return false;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private sealed class PlaintextTestTokenProtector : ITokenProtector
    {
        public string Protect(string token) => token;
        public string Unprotect(string protectedToken) => protectedToken;
    }

    private sealed class InstalledPrinterCatalog(string printerName) : IPrinterCatalog
    {
        public IReadOnlyList<PrinterDescriptor> GetInstalledPrinters() =>
            [new PrinterDescriptor(printerName, false)];

        public bool Exists(string candidate) => candidate.Equals(printerName, StringComparison.Ordinal);
    }

    private sealed class RecordingRawPrinter : IRawPrinter
    {
        private int _callCount;
        public int CallCount => _callCount;
        public byte[] LastData { get; private set; } = [];

        public Task PrintAsync(string printerName, string documentName, byte[] data, CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            LastData = data;
            return Task.CompletedTask;
        }
    }
}
