using Walos.PrintAgent.Api;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Tests;

public sealed class AgentStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "walos-print-agent-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RePairingDifferentIdentity_ClearsPreviousPrinterConfiguration()
    {
        var store = new AgentStateStore(_directory);
        await store.SetPairingAsync("token-1", new WorkstationIdentity(1, 2, "POS-1"), CancellationToken.None);
        await store.SetPrinterConfigurationAsync(
            new PrinterConfiguration(1, 2, "POS-1", "POS-58", 0, 50, 200),
            CancellationToken.None);

        await store.SetPairingAsync("token-2", new WorkstationIdentity(9, 8, "POS-2"), CancellationToken.None);

        Assert.Null(store.GetPrinterConfiguration());
    }

    [Fact]
    public async Task FailedDurableReservation_DoesNotMutateInMemoryLedger()
    {
        var blockedStatePath = Path.Combine(_directory, "agent-state.json");
        Directory.CreateDirectory(blockedStatePath);
        var store = new AgentStateStore(_directory);

        var persistenceError = await Record.ExceptionAsync(() => store.ReserveJobAsync(
            new StoredJob("job-not-durable", "open-drawer", "reserved", DateTimeOffset.UtcNow, null),
            CancellationToken.None));

        Assert.True(
            persistenceError is IOException or UnauthorizedAccessException,
            $"Se esperaba un error real de persistencia, se obtuvo {persistenceError?.GetType().Name ?? "ninguno"}.");
        Assert.Null(store.GetJob("job-not-durable"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
