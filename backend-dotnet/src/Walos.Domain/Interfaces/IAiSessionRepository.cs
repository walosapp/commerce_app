using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IAiSessionRepository
{
    Task<IAsyncDisposable> AcquireConversationLockAsync(long companyId, long userId);
    Task<AiSession> GetOrCreateSessionAsync(long companyId, long userId);
    Task<AiSession?> GetSessionAsync(long sessionId, long companyId, long userId);
    Task ResetSessionAsync(long sessionId, long companyId, long userId, string contextJson);
    Task UpdateSessionContextAsync(long sessionId, long companyId, long userId, string contextJson, string agentType);
    Task AddMessageAsync(long sessionId, long companyId, long userId, string role, string content, string metadataJson = "{}");
    Task<List<AiMessage>> GetMessagesAsync(long sessionId, long companyId, long userId, int limit = 20);
    Task TouchSessionAsync(long sessionId, long companyId, long userId);
    Task CleanupInactiveSessionsAsync(int inactiveMinutes = 30);
}
