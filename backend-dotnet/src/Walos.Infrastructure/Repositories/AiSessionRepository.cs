using Dapper;
using System.Data;
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

    public async Task<IAsyncDisposable> AcquireConversationLockAsync(long companyId, long userId)
    {
        var connection = await _db.CreateConnectionAsync();
        var transaction = connection.BeginTransaction();

        try
        {
            var lockKey = $"ai-conversation:{companyId}:{userId}";
            await connection.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0))",
                new { LockKey = lockKey },
                transaction);
            return new ConversationLock(connection, transaction);
        }
        catch
        {
            transaction.Dispose();
            connection.Dispose();
            throw;
        }
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
            await TouchSessionAsync(existing.Id, companyId, userId);
            return existing;
        }

        var id = await conn.QuerySingleAsync<long>(@"
            INSERT INTO core.ai_sessions (company_id, user_id, agent_type, context, last_activity_at)
            SELECT @CompanyId, @UserId, 'orchestrator', '{}', NOW()
            WHERE EXISTS (
                SELECT 1
                FROM core.users u
                WHERE u.id = @UserId
                  AND u.company_id = @CompanyId
                  AND u.is_active = TRUE
                  AND u.deleted_at IS NULL
            )
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

    public async Task<AiSession?> GetSessionAsync(long sessionId, long companyId, long userId)
    {
        using var conn = await _db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<AiSession>(@"
            SELECT id AS Id, company_id AS CompanyId, user_id AS UserId,
                   agent_type AS AgentType, context AS Context,
                   last_activity_at AS LastActivityAt, created_at AS CreatedAt
            FROM core.ai_sessions
            WHERE id = @SessionId AND company_id = @CompanyId AND user_id = @UserId",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId });
    }

    public async Task ResetSessionAsync(long sessionId, long companyId, long userId, string contextJson)
    {
        using var conn = await _db.CreateConnectionAsync();
        using var transaction = conn.BeginTransaction();

        var owned = await conn.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT 1 FROM core.ai_sessions
                WHERE id = @SessionId AND company_id = @CompanyId AND user_id = @UserId
            )",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId }, transaction);

        if (!owned)
        {
            transaction.Rollback();
            return;
        }

        await conn.ExecuteAsync("DELETE FROM core.ai_messages WHERE session_id = @SessionId",
            new { SessionId = sessionId }, transaction);
        await conn.ExecuteAsync(@"
            UPDATE core.ai_sessions
            SET context = @Context::jsonb, agent_type = 'orchestrator', last_activity_at = NOW()
            WHERE id = @SessionId AND company_id = @CompanyId AND user_id = @UserId",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId, Context = contextJson }, transaction);

        transaction.Commit();
    }

    public async Task UpdateSessionContextAsync(
        long sessionId,
        long companyId,
        long userId,
        string contextJson,
        string agentType)
    {
        using var conn = await _db.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            UPDATE core.ai_sessions
            SET context = @Context::jsonb, agent_type = @AgentType, last_activity_at = NOW()
            WHERE id = @SessionId AND company_id = @CompanyId AND user_id = @UserId",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId, Context = contextJson, AgentType = agentType });
    }

    public async Task AddMessageAsync(
        long sessionId,
        long companyId,
        long userId,
        string role,
        string content,
        string metadataJson = "{}")
    {
        using var conn = await _db.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO core.ai_messages (session_id, role, content, metadata)
            SELECT s.id, @Role, @Content, @Metadata::jsonb
            FROM core.ai_sessions s
            WHERE s.id = @SessionId AND s.company_id = @CompanyId AND s.user_id = @UserId",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId, Role = role, Content = content, Metadata = metadataJson });
    }

    public async Task<List<AiMessage>> GetMessagesAsync(
        long sessionId,
        long companyId,
        long userId,
        int limit = 20)
    {
        using var conn = await _db.CreateConnectionAsync();
        var msgs = await conn.QueryAsync<AiMessage>(@"
            SELECT m.id AS Id, m.session_id AS SessionId, m.role AS Role,
                   m.content AS Content, m.metadata AS Metadata, m.created_at AS CreatedAt
            FROM core.ai_messages m
            INNER JOIN core.ai_sessions s ON s.id = m.session_id
            WHERE m.session_id = @SessionId AND s.company_id = @CompanyId AND s.user_id = @UserId
            ORDER BY m.created_at ASC
            LIMIT @Limit",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId, Limit = limit });
        return msgs.ToList();
    }

    public async Task TouchSessionAsync(long sessionId, long companyId, long userId)
    {
        using var conn = await _db.CreateConnectionAsync();
        await conn.ExecuteAsync(@"
            UPDATE core.ai_sessions SET last_activity_at = NOW()
            WHERE id = @SessionId AND company_id = @CompanyId AND user_id = @UserId",
            new { SessionId = sessionId, CompanyId = companyId, UserId = userId });
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

    private sealed class ConversationLock : IAsyncDisposable
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private bool _disposed;

        public ConversationLock(IDbConnection connection, IDbTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;

            try
            {
                _transaction.Commit();
            }
            finally
            {
                _transaction.Dispose();
                _connection.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }
}
