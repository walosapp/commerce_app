using Walos.PrintAgent.Api;
using Walos.PrintAgent.Commands;
using Walos.PrintAgent.Printing;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Tests;

public sealed class PrintCommandServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "walos-print-agent-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MissingInstalledPrinter_IsRejectedBeforeSpooling()
    {
        var store = new AgentStateStore(_directory);
        await store.SetPairingAsync(
            "protected-test-token",
            new WorkstationIdentity(1, 2, "POS-1"),
            CancellationToken.None);
        await store.SetPrinterConfigurationAsync(
            new PrinterConfiguration(1, 2, "POS-1", "No existe", 0, 50, 200),
            CancellationToken.None);
        var spooler = new FakeRawPrinter();
        var service = new PrintCommandService(store, new EmptyPrinterCatalog(), spooler, new EscPos58Encoder());

        Assert.Throws<PrinterNotFoundException>(() => service.PrepareTestTicket());
        Assert.Equal(0, spooler.CallCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private sealed class EmptyPrinterCatalog : IPrinterCatalog
    {
        public IReadOnlyList<PrinterDescriptor> GetInstalledPrinters() => [];
        public bool Exists(string printerName) => false;
    }

    private sealed class FakeRawPrinter : IRawPrinter
    {
        public int CallCount { get; private set; }

        public Task PrintAsync(string printerName, string documentName, byte[] data, CancellationToken ct)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
