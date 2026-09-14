using Walos.PrintAgent.Commands;
using Walos.PrintAgent.Printing;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Tests;

public sealed class IdempotentJobExecutorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "walos-print-agent-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SameJobId_ExecutesPhysicalOperationOnlyOnce()
    {
        var executor = new IdempotentJobExecutor(new AgentStateStore(_directory));
        var executions = 0;

        var first = executor.ExecuteAsync("job-1", "open-drawer", _ =>
        {
            Interlocked.Increment(ref executions);
            return Task.CompletedTask;
        }, CancellationToken.None);
        var second = executor.ExecuteAsync("job-1", "open-drawer", _ =>
        {
            Interlocked.Increment(ref executions);
            return Task.CompletedTask;
        }, CancellationToken.None);

        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, executions);
        Assert.Single(results, result => result.Executed);
        Assert.Single(results, result => result.Status == "replayed");
    }

    [Fact]
    public async Task FailedOperation_ConsumesJobIdToPreserveAtMostOnce()
    {
        var executor = new IdempotentJobExecutor(new AgentStateStore(_directory));

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            "retryable-job",
            "test-print",
            _ => throw new InvalidOperationException("spooler unavailable"),
            CancellationToken.None));

        var result = await executor.ExecuteAsync(
            "retryable-job",
            "test-print",
            _ => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("failed", result.Status);
    }

    [Fact]
    public async Task CompletedJob_IsStillIdempotentAfterStoreReconstruction()
    {
        var executions = 0;
        var firstExecutor = new IdempotentJobExecutor(new AgentStateStore(_directory));
        await firstExecutor.ExecuteAsync(
            "durable-job",
            "test-print",
            _ =>
            {
                executions++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        var restartedExecutor = new IdempotentJobExecutor(new AgentStateStore(_directory));
        var replay = await restartedExecutor.ExecuteAsync(
            "durable-job",
            "test-print",
            _ =>
            {
                executions++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(1, executions);
        Assert.False(replay.Executed);
        Assert.Equal("replayed", replay.Status);
    }

    [Fact]
    public async Task SameJobIdForDifferentCommand_IsRejected()
    {
        var executor = new IdempotentJobExecutor(new AgentStateStore(_directory));
        await executor.ExecuteAsync("shared-job", "test-print", _ => Task.CompletedTask, CancellationToken.None);

        await Assert.ThrowsAsync<JobConflictException>(() => executor.ExecuteAsync(
            "shared-job",
            "open-drawer",
            _ => Task.CompletedTask,
            CancellationToken.None));
    }

    [Fact]
    public async Task ReservedJobAfterRestart_IsUncertainAndDoesNotExecuteAgain()
    {
        var store = new AgentStateStore(_directory);
        await store.ReserveJobAsync(
            new StoredJob("reserved-job", "open-drawer", "reserved", DateTimeOffset.UtcNow, null),
            CancellationToken.None);
        var executions = 0;

        var result = await new IdempotentJobExecutor(new AgentStateStore(_directory)).ExecuteAsync(
            "reserved-job",
            "open-drawer",
            _ =>
            {
                executions++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(0, executions);
        Assert.False(result.Executed);
        Assert.Equal("uncertain", result.Status);
    }

    [Fact]
    public async Task PreflightFailure_DoesNotConsumeJobId()
    {
        var executor = new IdempotentJobExecutor(new AgentStateStore(_directory));

        await Assert.ThrowsAsync<PrinterNotConfiguredException>(() => executor.ExecuteAsync<bool>(
            "preflight-job",
            "test-print",
            () => throw new PrinterNotConfiguredException("missing"),
            (_, _) => Task.CompletedTask,
            CancellationToken.None));

        var result = await executor.ExecuteAsync(
            "preflight-job",
            "test-print",
            static () => true,
            static (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.True(result.Executed);
        Assert.Equal("completed", result.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
