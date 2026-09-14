using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Commands;

public sealed record JobExecutionResult(string Status, bool Executed);

public sealed class JobConflictException(string jobId)
    : Exception($"El jobId '{jobId}' ya fue utilizado para otro comando.");

public sealed class IdempotentJobExecutor(AgentStateStore store)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<JobExecutionResult> ExecuteAsync(
        string jobId,
        string command,
        Func<CancellationToken, Task> operation,
        CancellationToken ct) => await ExecuteAsync(
            jobId,
            command,
            static () => true,
            async (_, token) => await operation(token),
            ct);

    public async Task<JobExecutionResult> ExecuteAsync<TPrepared>(
        string jobId,
        string command,
        Func<TPrepared> prepare,
        Func<TPrepared, CancellationToken, Task> operation,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var previous = store.GetJob(jobId);
            if (previous is not null)
            {
                if (!previous.Command.Equals(command, StringComparison.Ordinal))
                {
                    throw new JobConflictException(jobId);
                }

                return new JobExecutionResult(
                    previous.Status switch
                    {
                        "completed" => "replayed",
                        "failed" => "failed",
                        _ => "uncertain"
                    },
                    false);
            }

            // Validate and materialize the typed command before consuming the jobId.
            // This stage must not touch the spooler or any physical device.
            var prepared = prepare();

            // Reserve durably BEFORE touching hardware. If the process crashes after the
            // physical side effect, the same jobId remains consumed and cannot duplicate it.
            await store.ReserveJobAsync(new StoredJob(
                jobId,
                command,
                "reserved",
                DateTimeOffset.UtcNow,
                null), ct);
            try
            {
                await operation(prepared, ct);
                await store.MarkJobCompletedAsync(jobId, ct);
                return new JobExecutionResult("completed", true);
            }
            catch
            {
                // Once spooling begins, the physical outcome can be uncertain. Consume the
                // job durably and report failure rather than risking a duplicate on retry.
                await store.MarkJobFailedAsync(jobId, CancellationToken.None);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
