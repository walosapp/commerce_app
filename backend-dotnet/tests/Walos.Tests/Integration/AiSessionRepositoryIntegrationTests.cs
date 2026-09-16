using Microsoft.Extensions.Logging.Abstractions;
using Walos.Infrastructure.Repositories;

namespace Walos.Tests.Integration;

[Collection("PostgreSQL Integration Tests")]
public sealed class AiSessionRepositoryIntegrationTests : V1IntegrationTestBase
{
    [SkippableFact]
    public async Task Session_Lookup_And_Reset_Are_Scoped_To_Company_And_User()
    {
        var companyId = await SeedCompanyAsync("AI session scope");
        var branchId = await SeedBranchAsync(companyId);
        var ownerId = await SeedUserAsync(companyId, branchId);
        var otherUserId = await SeedUserAsync(companyId, branchId);
        var foreignCompanyId = await SeedCompanyAsync("AI foreign session scope");
        var foreignBranchId = await SeedBranchAsync(foreignCompanyId);
        var foreignUserId = await SeedUserAsync(foreignCompanyId, foreignBranchId);
        var repository = new AiSessionRepository(
            ConnectionFactory,
            NullLogger<AiSessionRepository>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetOrCreateSessionAsync(companyId, foreignUserId));

        var session = await repository.GetOrCreateSessionAsync(companyId, ownerId);
        await repository.AddMessageAsync(session.Id, companyId, ownerId, "user", "owner secret");
        await repository.AddMessageAsync(session.Id, companyId, otherUserId, "user", "cross-user write");
        await repository.AddMessageAsync(session.Id, foreignCompanyId, foreignUserId, "user", "cross-tenant write");
        await repository.UpdateSessionContextAsync(
            session.Id, companyId, otherUserId, "{\"cross_user\":true}", "inventory");
        await repository.UpdateSessionContextAsync(
            session.Id, foreignCompanyId, foreignUserId, "{\"cross_tenant\":true}", "inventory");

        Assert.Null(await repository.GetSessionAsync(session.Id, companyId, otherUserId));
        Assert.Null(await repository.GetSessionAsync(session.Id, foreignCompanyId, foreignUserId));
        await repository.ResetSessionAsync(session.Id, companyId, otherUserId, "{}");
        await repository.ResetSessionAsync(session.Id, foreignCompanyId, foreignUserId, "{}");
        Assert.Empty(await repository.GetMessagesAsync(session.Id, companyId, otherUserId));
        Assert.Empty(await repository.GetMessagesAsync(session.Id, foreignCompanyId, foreignUserId));
        Assert.Single(await repository.GetMessagesAsync(session.Id, companyId, ownerId));
        var unchanged = await repository.GetSessionAsync(session.Id, companyId, ownerId);
        Assert.DoesNotContain("cross_user", unchanged!.Context);
        Assert.DoesNotContain("cross_tenant", unchanged.Context);

        await repository.ResetSessionAsync(
            session.Id,
            companyId,
            ownerId,
            "{\"capability_fingerprint\":\"new\"}");

        Assert.Empty(await repository.GetMessagesAsync(session.Id, companyId, ownerId));
        var owned = await repository.GetSessionAsync(session.Id, companyId, ownerId);
        Assert.NotNull(owned);
        Assert.Contains("capability_fingerprint", owned!.Context);
    }

    [SkippableFact]
    public async Task Conversation_Lock_Serializes_Concurrent_Chats_For_The_Same_Owner()
    {
        var companyId = await SeedCompanyAsync("AI conversation lock");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId);
        var repository = new AiSessionRepository(
            ConnectionFactory,
            NullLogger<AiSessionRepository>.Instance);

        await using var first = await repository.AcquireConversationLockAsync(companyId, userId);
        var secondTask = repository.AcquireConversationLockAsync(companyId, userId);

        await Task.Delay(150);
        Assert.False(secondTask.IsCompleted);

        await first.DisposeAsync();
        await using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
