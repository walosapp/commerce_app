using Walos.PrintAgent.Api;
using Walos.PrintAgent.Security;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Tests;

public sealed class PairingServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "walos-print-agent-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PairingCode_IsOneTime()
    {
        var pairing = CreatePairing();
        var code = pairing.CurrentPairingCode;
        var request = new PairRequest(code, 1, 2, "POS-1");

        Assert.NotNull(await pairing.PairAsync(request, CancellationToken.None));
        Assert.Null(await pairing.PairAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredPairingCode_IsRejected()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var pairing = CreatePairing(time, TimeSpan.FromMinutes(1));
        var expiredCode = pairing.CurrentPairingCode;
        time.Advance(TimeSpan.FromMinutes(2));

        var result = await pairing.PairAsync(
            new PairRequest(expiredCode, 1, 2, "POS-1"),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CorruptProtectedToken_IsRejectedWithoutThrowing()
    {
        var store = new AgentStateStore(_directory);
        await store.SetPairingAsync(
            "not-valid-base64",
            new WorkstationIdentity(1, 2, "POS-1"),
            CancellationToken.None);
        var pairing = new PairingService(store, new DpapiTokenProtector());

        Assert.False(pairing.ValidateToken("candidate"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private PairingService CreatePairing(
        TimeProvider? timeProvider = null,
        TimeSpan? lifetime = null) => new(
            new AgentStateStore(_directory),
            new PlaintextTokenProtector(),
            timeProvider ?? TimeProvider.System,
            lifetime ?? PairingService.DefaultPairingCodeLifetime);

    private sealed class PlaintextTokenProtector : ITokenProtector
    {
        public string Protect(string token) => token;
        public string Unprotect(string protectedToken) => protectedToken;
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
