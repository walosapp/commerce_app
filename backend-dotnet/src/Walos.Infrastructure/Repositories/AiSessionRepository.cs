using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class AiSessionRepository : IAiSessionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<AiSessionRepository> _logger;

    public AiSessionRepository(IDbConnectionFactory db, ILogger<AiSessionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AiSession> GetOrCreateSessionAsync(long companyId, long userId)
    {
        using var conn = await _db.CreateConnectionAsync();

        var existing = await conn.QuerySingleOrDefaultAsync<AiSession>(@"
            SELECT id AS Id, company_id AS CompanyId, user_id AS UserId,
                   agent_type AS AgentType, context AS Context,
                   last_activity_at AS LastActivityAt, created_at AS CreatedAt
            FROM core.ai_sessions
            WHERE company_id = @CompanyId AND user_id = @UserId
              AND last_activity_at >= NOW() - INTERVAL '30 minutes'
            ORDER BY last_activity_at DESC
            LIMIT 1",
            new { CompanyId = companyId, UserId = userId });

        if (existing != null)
        {
            await TouchSessionAsync(existing.Id);
            return existing;
        }

        var id = await conn.QuerySingleAsync<long>(@"
            INSERT INTO core.ai_sessions (company_id, user_id, agent_type, context, last_activity_at)
            VALUES (@CompanyId, @UserId, 'orchestrator', '{}', NOW())
            RETURNING id",
            new { CompanyId = companyId, UserId = userId });

        return new AiSession
        {
            Id = id,
            CompanyId = companyId,
            UserId = userId,
            AgentType = "orchestrator",
            Context = "{}",
            LastActivityAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<AiSession?> GetSessionAsync(long sessionId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<AiSession>(@"
            SELECT id AS Id, company_id AS CompanyId, user_id AS UserId,
                   agent_type AS AgentType, context AS Context,
                   last_activity_at AS LastActivityAt, created_at AS CreatedAt
            FROM core.ai_sessions
            WHERE id = @SessionId AND company_id = @CompanyId",
            new { SessionId = sessionId, CompanyId = companyId });
    }

    public async Task UpdateSessionContextAsync(long sessionId, string contextJson, string agentType)
    {
        using var conn = await _db.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            UPDATE core.ai_sessions
            SET context = @Context::jsonb, agent_type = @AgentType, last_activity_at = NOW()
            WHERE id = @SessionId",
            new { SessionId = sessionId, Context = contextJson, AgentType = agentType });
    }

    public async Task AddMessageAsync(long sessionId, string role, string content, string metadataJson = "{}")
    {
        using var conn = await _db.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO core.ai_messages (session_id, role, content, metadata)
            VALUES (@SessionId, @Role, @Content, @Metadata::jsonb)",
            new { SessionId = sessionId, Role = role, Content = content, Metadata = metadataJson });
    }

    public async Task<List<AiMessage>> GetMessagesAsync(long sessionId, int limit = 20)
    {
        using var conn = await _db.CreateConnectionAsync();
        var msgs = await conn.QueryAsync<AiMessage>(@"
            SELECT id AS Id, session_id AS SessionId, role AS Role,
                   content AS Content, metadata AS Metadata, created_at AS CreatedAt
            FROM core.ai_messages
            WHERE session_id = @SessionId
            ORDER BY created_at ASC
            LIMIT @Limit",
            new { SessionId = sessionId, Limit = limit });
        return msgs.ToList();
    }

    public async Task TouchSessionAsync(long sessionId)
    {
        using var conn = await _db.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            UPDATE core.ai_sessions SET last_activity_at = NOW() WHERE id = @SessionId",
            new { SessionId = sessionId });
    }

    public async Task CleanupInactiveSessionsAsync(int inactiveMinutes = 30)
    {
        using var conn = await _db.CreateConnectionAsync();
        var deleted = await conn.ExecuteAsync(@"
            DELETE FROM core.ai_sessions
            WHERE last_activity_at < NOW() - (INTERVAL '1 minute' * @Minutes)",
            new { Minutes = inactiveMinutes });

        if (deleted > 0)
            _logger.LogInformation("AI session cleanup: {Count} sesiones inactivas eliminadas", deleted);
    }
}
