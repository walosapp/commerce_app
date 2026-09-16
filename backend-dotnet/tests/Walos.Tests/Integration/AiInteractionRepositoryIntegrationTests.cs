using Dapper;
using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

[Collection("PostgreSQL Integration Tests")]
public sealed class AiInteractionRepositoryIntegrationTests : V1IntegrationTestBase
{
    [SkippableFact]
    public async Task SaveAiInteraction_Rejects_CrossTenant_User_And_Branch_Ownership()
    {
        var companyId = await SeedCompanyAsync("AI interaction owner");
        var branchId = await SeedBranchAsync(companyId);
        var userId = await SeedUserAsync(companyId, branchId);
        var foreignCompanyId = await SeedCompanyAsync("AI interaction foreign");
        var foreignBranchId = await SeedBranchAsync(foreignCompanyId);
        var foreignUserId = await SeedUserAsync(foreignCompanyId, foreignBranchId);

        var saved = await InventoryRepository.SaveAiInteractionAsync(
            Interaction(companyId, branchId, userId, "valid"));

        Assert.True(saved.Id > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InventoryRepository.SaveAiInteractionAsync(
                Interaction(companyId, branchId, foreignUserId, "foreign-user")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InventoryRepository.SaveAiInteractionAsync(
                Interaction(companyId, foreignBranchId, userId, "foreign-branch")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InventoryRepository.SaveAiInteractionAsync(
                Interaction(foreignCompanyId, branchId, userId, "foreign-company")));

        using var connection = await ConnectionFactory.CreateConnectionAsync();
        var count = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(*)::int
            FROM inventory.ai_interactions
            WHERE session_id IN ('valid', 'foreign-user', 'foreign-branch', 'foreign-company')");
        Assert.Equal(1, count);
    }

    private static AiInteraction Interaction(
        long companyId,
        long branchId,
        long userId,
        string sessionId) => new()
        {
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            SessionId = sessionId,
            InteractionType = "text",
            UserInput = "consulta",
            AiResponse = "respuesta",
            AiAction = "query",
            ProcessedData = "{}",
            ActionStatus = "pending",
            ConfidenceScore = 100,
            AiModel = "test",
            TokensUsed = 1
        };
}
