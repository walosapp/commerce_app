using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface IAiSessionRepository
{
    Task<AiSession> GetOrCreateSessionAsync(long companyId, long userId);
    Task<AiSession?> GetSessionAsync(long sessionId, long companyId);
    Task UpdateSessionContextAsync(long sessionId, string contextJson, string agentType);
    Task AddMessageAsync(long sessionId, string role, string content, string metadataJson = "{}");
    Task<List<AiMessage>> GetMessagesAsync(long sessionId, int limit = 20);
    Task TouchSessionAsync(long sessionId);
    Task CleanupInactiveSessionsAsync(int inactiveMinutes = 30);
}
