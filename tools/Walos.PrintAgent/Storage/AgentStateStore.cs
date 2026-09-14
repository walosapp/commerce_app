using System.Text.Json;
using Walos.PrintAgent.Api;

namespace Walos.PrintAgent.Storage;

public static class AgentPaths
{
    public static string GetStateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Walos",
        "PrintAgent");
}

public sealed record WorkstationIdentity(long CompanyId, long BranchId, string WorkstationId);

public sealed record StoredJob(
    string JobId,
    string Command,
    string Status,
    DateTimeOffset ReservedAtUtc,
    DateTimeOffset? CompletedAtUtc);

internal sealed class AgentState
{
    public Guid AgentId { get; set; } = Guid.NewGuid();
    public string? ProtectedToken { get; set; }
    public WorkstationIdentity? Identity { get; set; }
    public PrinterConfiguration? Printer { get; set; }
    public List<StoredJob> Jobs { get; set; } = [];
}

public sealed class AgentStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile AgentState _state;

    public AgentStateStore(string stateDirectory)
    {
        Directory.CreateDirectory(stateDirectory);
        _path = Path.Combine(stateDirectory, "agent-state.json");
        _state = Load(_path);
    }

    public Guid AgentId => _state.AgentId;
    public bool IsPaired => !string.IsNullOrEmpty(_state.ProtectedToken);

    public string? GetProtectedToken() => _state.ProtectedToken;

    public PrinterConfiguration? GetPrinterConfiguration() => _state.Printer;

    public bool MatchesPairedIdentity(long companyId, long branchId, string workstationId)
    {
        var identity = _state.Identity;
        return identity is not null &&
               identity.CompanyId == companyId &&
               identity.BranchId == branchId &&
               identity.WorkstationId.Equals(workstationId, StringComparison.Ordinal);
    }

    public StoredJob? GetJob(string jobId) =>
        _state.Jobs.FirstOrDefault(job => job.JobId.Equals(jobId, StringComparison.Ordinal));

    public async Task SetPairingAsync(
        string protectedToken,
        WorkstationIdentity identity,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var next = Clone(_state);
            if (next.Identity is not null && next.Identity != identity)
            {
                // A printer selection belongs to one company/branch/workstation only.
                // Never carry it into a newly paired tenant or physical workstation.
                next.Printer = null;
            }

            next.ProtectedToken = protectedToken;
            next.Identity = identity;
            await CommitAsync(next, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetPrinterConfigurationAsync(PrinterConfiguration configuration, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var next = Clone(_state);
            next.Printer = configuration;
            await CommitAsync(next, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReserveJobAsync(StoredJob job, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var next = Clone(_state);
            next.Jobs.Add(job);
            await CommitAsync(next, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkJobCompletedAsync(string jobId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var next = Clone(_state);
            var index = next.Jobs.FindIndex(job => job.JobId.Equals(jobId, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new InvalidOperationException($"No existe la reserva para el jobId '{jobId}'.");
            }

            next.Jobs[index] = next.Jobs[index] with
            {
                Status = "completed",
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
            await CommitAsync(next, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkJobFailedAsync(string jobId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var next = Clone(_state);
            var index = next.Jobs.FindIndex(job => job.JobId.Equals(jobId, StringComparison.Ordinal));
            if (index < 0)
            {
                throw new InvalidOperationException($"No existe la reserva para el jobId '{jobId}'.");
            }

            next.Jobs[index] = next.Jobs[index] with { Status = "failed" };
            await CommitAsync(next, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static AgentState Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AgentState();
        }

        try
        {
            return JsonSerializer.Deserialize<AgentState>(File.ReadAllText(path), JsonOptions) ?? new AgentState();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"La configuración local está dañada: {path}", exception);
        }
    }

    private static AgentState Clone(AgentState source) => new()
    {
        AgentId = source.AgentId,
        ProtectedToken = source.ProtectedToken,
        Identity = source.Identity,
        Printer = source.Printer,
        Jobs = [.. source.Jobs]
    };

    private async Task CommitAsync(AgentState next, CancellationToken ct)
    {
        var temporaryPath = _path + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.WriteThrough | FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, next, JsonOptions, ct);
            await stream.FlushAsync(ct);
        }

        File.Move(temporaryPath, _path, true);
        _state = next;
    }
}
