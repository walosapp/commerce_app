using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Walos.PrintAgent.Api;
using Walos.PrintAgent.Printing;
using Walos.PrintAgent.Security;

namespace Walos.PrintAgent.Tests;

public sealed class ApiSecurityTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "walos-print-agent-tests", Guid.NewGuid().ToString("N"));
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
        builder.Services.AddSingleton<IPrinterCatalog, EmptyPrinterCatalog>();

        _application = builder.Build();
        PrintAgentApi.ConfigurePipeline(_application);
        await _application.StartAsync();
        _client = _application.GetTestClient();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("https://evil.test")]
    [InlineData("http://remote-walos.test")]
    public async Task InvalidOrigin_IsRejected(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
        request.Headers.Add("Origin", origin);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("https://evil.test")]
    [InlineData("http://remote-walos.test")]
    public async Task InvalidOrigin_PreflightIsRejectedWithoutCorsHeader(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/printers");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("https://commerce-app-red.vercel.app")]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    public async Task CanonicalOrigin_GetHealthSucceedsWithExactCorsHeader(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
        request.Headers.Add("Origin", origin);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Theory]
    [InlineData("https://commerce-app-red.vercel.app")]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1:5173")]
    public async Task CanonicalOrigin_PreflightSucceedsWithExactCorsHeader(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/printers");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task OfficialOrigin_ProtectedGetPreflightAllowsAuthorizationHeader()
    {
        const string origin = "https://commerce-app-red.vercel.app";
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/printers");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(
            response.Headers.GetValues("Access-Control-Allow-Headers"),
            value => value.Split(',').Any(header =>
                header.Trim().Equals("Authorization", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task OfficialOrigin_PairPostPreflightAllowsContentTypeHeader()
    {
        const string origin = "https://commerce-app-red.vercel.app";
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/pair");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(
            response.Headers.GetValues("Access-Control-Allow-Headers"),
            value => value.Split(',').Any(header =>
                header.Trim().Equals("Content-Type", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ConfiguredOrigin_IsAdditiveAndDoesNotReplaceCanonicalOrigins()
    {
        foreach (var origin in new[] { "https://walos.test", "https://commerce-app-red.vercel.app" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
            request.Headers.Add("Origin", origin);

            using var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        }
    }

    [Fact]
    public async Task CanonicalOrigins_AreAllowedWhenNoAdditionalOriginsAreConfigured()
    {
        var directory = Path.Combine(Path.GetTempPath(), "walos-print-agent-tests", Guid.NewGuid().ToString("N"));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        PrintAgentApi.ConfigureServices(builder.Services, builder.Configuration, directory);
        var application = builder.Build();
        PrintAgentApi.ConfigurePipeline(application);
        await application.StartAsync();
        using var client = application.GetTestClient();

        try
        {
            foreach (var origin in new[]
                     {
                         "https://commerce-app-red.vercel.app",
                         "http://localhost:5173",
                         "http://127.0.0.1:5173"
                     })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/health");
                request.Headers.Add("Origin", origin);
                using var response = await client.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            }
        }
        finally
        {
            await application.DisposeAsync();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void RemoteHttpAllowedOrigin_IsRejectedAtStartup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["WALOS_PRINT_AGENT_ALLOWED_ORIGINS"] = "http://walos.test";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PrintAgentApi.ConfigureServices(builder.Services, builder.Configuration, _directory));

        Assert.Contains("origen", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WildcardAllowedOrigin_IsRejectedAtStartup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["WALOS_PRINT_AGENT_ALLOWED_ORIGINS"] = "*";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PrintAgentApi.ConfigureServices(builder.Services, builder.Configuration, _directory));

        Assert.Contains("origen", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_ReportsSemanticProductVersion()
    {
        using var response = await _client.GetAsync("/v1/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1.0.2", body.GetProperty("version").GetString());
    }

    [Fact]
    public async Task InvalidToken_IsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/printers");
        request.Headers.Add("Origin", "https://walos.test");
        request.Headers.Authorization = new("Bearer", "invalid");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrigin_IsRejectedOnSensitiveEndpoint()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/printers");
        request.Headers.Authorization = new("Bearer", "invalid");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExcessivePayload_IsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pair");
        request.Headers.Add("Origin", "https://walos.test");
        request.Content = JsonContent.Create(new { payload = new string('x', 9_000) });

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("https://walos.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task ExcessiveChunkedPayload_IsRejectedWithReadableCorsResponse()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pair");
        request.Headers.Add("Origin", "https://walos.test");
        request.Content = new UnknownLengthJsonContent(new string('x', 9_000));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("https://walos.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task UnsupportedContentType_IsRejectedWithReadableCorsResponse()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pair");
        request.Headers.Add("Origin", "https://walos.test");
        request.Content = new StringContent("<html></html>", Encoding.UTF8, "text/html");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("https://walos.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task AllowedOrigin_PreflightSucceeds()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/printers");
        request.Headers.Add("Origin", "https://walos.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://walos.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task UnknownJsonFields_AreRejected()
    {
        var pairing = _application.Services.GetRequiredService<PairingService>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pair");
        request.Headers.Add("Origin", "https://walos.test");
        request.Content = JsonContent.Create(new
        {
            pairingCode = pairing.CurrentPairingCode,
            companyId = 1,
            branchId = 2,
            workstationId = "POS-1",
            rawEscPos = "G0A="
        });

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pair_ReturnsTokenThatAuthorizesProtectedEndpoints()
    {
        var pairing = _application.Services.GetRequiredService<PairingService>();
        using var pairRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/pair");
        pairRequest.Headers.Add("Origin", "https://walos.test");
        pairRequest.Content = JsonContent.Create(new
        {
            pairingCode = pairing.CurrentPairingCode,
            companyId = 1,
            branchId = 2,
            workstationId = "POS-1"
        });

        using var pairResponse = await _client.SendAsync(pairRequest);
        var pairJson = await pairResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = pairJson.GetProperty("token").GetString();

        Assert.Equal(HttpStatusCode.OK, pairResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var printersRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/printers");
        printersRequest.Headers.Add("Origin", "https://walos.test");
        printersRequest.Headers.Authorization = new("Bearer", token);
        using var printersResponse = await _client.SendAsync(printersRequest);

        Assert.Equal(HttpStatusCode.OK, printersResponse.StatusCode);
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

    private sealed class EmptyPrinterCatalog : IPrinterCatalog
    {
        public IReadOnlyList<PrinterDescriptor> GetInstalledPrinters() => [];
        public bool Exists(string printerName) => false;
    }

    private sealed class UnknownLengthJsonContent : HttpContent
    {
        private readonly byte[] _payload;

        public UnknownLengthJsonContent(string payload)
        {
            _payload = Encoding.UTF8.GetBytes($"{{\"payload\":\"{payload}\"}}");
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_payload).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
