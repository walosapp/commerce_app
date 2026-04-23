using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Walos.Domain.Entities.Platform;

namespace Walos.Infrastructure.Repositories;

public partial class PlatformRepository
{
    public async Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync(long companyId)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, type AS Type, provider AS Provider,
                       last4 AS Last4, bank_name AS BankName, holder_name AS HolderName,
                       is_default AS IsDefault, is_active AS IsActive,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM platform.payment_methods
                WHERE company_id = @CompanyId AND is_active = TRUE
                ORDER BY is_default DESC, created_at DESC";

            return await conn.QueryAsync<PaymentMethod>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo metodos de pago empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<PaymentMethod> CreatePaymentMethodAsync(PaymentMethod method)
    {
        try
        {
            using var conn = (NpgsqlConnection)await _connectionFactory.CreateConnectionAsync();
            using var tx = await conn.BeginTransactionAsync();

            if (method.IsDefault)
            {
                await conn.ExecuteAsync(
                    "UPDATE platform.payment_methods SET is_default = FALSE, updated_at = NOW() WHERE company_id = @CompanyId",
                    new { method.CompanyId }, tx);
            }

            const string sql = @"
                INSERT INTO platform.payment_methods
                    (company_id, type, provider, provider_token, last4, bank_name, holder_name, is_default)
                VALUES
                    (@CompanyId, @Type, @Provider, @ProviderToken, @Last4, @BankName, @HolderName, @IsDefault)
                RETURNING id AS Id, company_id AS CompanyId, type AS Type, provider AS Provider,
                          last4 AS Last4, bank_name AS BankName, holder_name AS HolderName,
                          is_default AS IsDefault, is_active AS IsActive,
                          created_at AS CreatedAt, updated_at AS UpdatedAt";

            var created = await conn.QuerySingleAsync<PaymentMethod>(sql, method, tx);
            await tx.CommitAsync();
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando metodo de pago empresa {CompanyId}", method.CompanyId);
            throw;
        }
    }

    public async Task SetDefaultPaymentMethodAsync(long id, long companyId)
    {
        try
        {
            using var conn = (NpgsqlConnection)await _connectionFactory.CreateConnectionAsync();
            using var tx = await conn.BeginTransactionAsync();

            await conn.ExecuteAsync(
                "UPDATE platform.payment_methods SET is_default = FALSE, updated_at = NOW() WHERE company_id = @CompanyId",
                new { CompanyId = companyId }, tx);

            await conn.ExecuteAsync(
                "UPDATE platform.payment_methods SET is_default = TRUE,  updated_at = NOW() WHERE id = @Id AND company_id = @CompanyId",
                new { Id = id, CompanyId = companyId }, tx);

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estableciendo metodo de pago por defecto {Id}", id);
            throw;
        }
    }

    public async Task DeletePaymentMethodAsync(long id, long companyId)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE platform.payment_methods
                SET is_active = FALSE, updated_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId";

            await conn.ExecuteAsync(sql, new { Id = id, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando metodo de pago {Id}", id);
            throw;
        }
    }

    public async Task<CompanyAiSettings> GetAiSettingsAsync(long companyId)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS CompanyId,
                       ai_key_managed    AS AiKeyManaged,
                       ai_provider       AS AiProvider,
                       (ai_api_key_enc IS NOT NULL AND ai_api_key_enc <> '') AS HasCustomKey,
                       ai_tokens_used    AS AiTokensUsed,
                       ai_tokens_reset_at AS AiTokensResetAt,
                       ai_estimated_cost AS AiEstimatedCost
                FROM core.companies
                WHERE id = @CompanyId AND deleted_at IS NULL";

            return await conn.QuerySingleAsync<CompanyAiSettings>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuracion AI empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task UpdateAiKeyAsync(long companyId, string? encryptedKey, string provider, bool managed)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE core.companies
                SET ai_api_key_enc  = @EncryptedKey,
                    ai_provider     = @Provider,
                    ai_key_managed  = @Managed,
                    updated_at      = NOW()
                WHERE id = @CompanyId AND deleted_at IS NULL";

            await conn.ExecuteAsync(sql, new { CompanyId = companyId, EncryptedKey = encryptedKey, Provider = provider, Managed = managed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando API key IA empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task IncrementAiTokensAsync(long companyId, long tokens, decimal cost)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE core.companies
                SET ai_tokens_used      = ai_tokens_used + @Tokens,
                    ai_estimated_cost   = ai_estimated_cost + @Cost,
                    updated_at          = NOW()
                WHERE id = @CompanyId AND deleted_at IS NULL";

            await conn.ExecuteAsync(sql, new { CompanyId = companyId, Tokens = tokens, Cost = cost });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementando tokens AI empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task ResetAiTokensAsync(long companyId)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE core.companies
                SET ai_tokens_used      = 0,
                    ai_estimated_cost   = 0,
                    ai_tokens_reset_at  = NOW(),
                    updated_at          = NOW()
                WHERE id = @CompanyId AND deleted_at IS NULL";

            await conn.ExecuteAsync(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reseteando tokens AI empresa {CompanyId}", companyId);
            throw;
        }
    }
}
