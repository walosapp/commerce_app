using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities.Platform;

namespace Walos.Infrastructure.Repositories;

public partial class PlatformRepository
{
    public async Task<IEnumerable<ServiceCatalog>> GetServiceCatalogAsync()
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS Id, code AS Code, name AS Name, description AS Description,
                       base_price AS BasePrice, billing_unit AS BillingUnit,
                       is_active AS IsActive, display_order AS DisplayOrder,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM platform.service_catalog
                WHERE is_active = TRUE
                ORDER BY display_order ASC, name ASC";

            return await conn.QueryAsync<ServiceCatalog>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo catalogo de servicios");
            throw;
        }
    }

    public async Task<IEnumerable<CompanySubscription>> GetCompanySubscriptionsAsync(long companyId)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT
                    cs.id AS Id, cs.company_id AS CompanyId, cs.service_code AS ServiceCode,
                    cs.is_active AS IsActive, cs.custom_price AS CustomPrice,
                    cs.billing_frequency AS BillingFrequency, cs.next_billing_date AS NextBillingDate,
                    cs.started_at AS StartedAt, cs.cancelled_at AS CancelledAt,
                    cs.notes AS Notes, cs.created_at AS CreatedAt, cs.updated_at AS UpdatedAt,
                    sc.name AS ServiceName, sc.base_price AS BasePrice
                FROM platform.company_subscriptions cs
                INNER JOIN platform.service_catalog sc ON sc.code = cs.service_code
                WHERE cs.company_id = @CompanyId
                ORDER BY sc.display_order ASC";

            return await conn.QueryAsync<CompanySubscription>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo suscripciones de empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<CompanySubscription?> GetSubscriptionAsync(long companyId, string serviceCode)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT cs.id AS Id, cs.company_id AS CompanyId, cs.service_code AS ServiceCode,
                       cs.is_active AS IsActive, cs.custom_price AS CustomPrice,
                       cs.billing_frequency AS BillingFrequency, cs.next_billing_date AS NextBillingDate,
                       cs.started_at AS StartedAt, cs.cancelled_at AS CancelledAt,
                       cs.notes AS Notes, cs.created_at AS CreatedAt, cs.updated_at AS UpdatedAt,
                       sc.name AS ServiceName, sc.base_price AS BasePrice
                FROM platform.company_subscriptions cs
                INNER JOIN platform.service_catalog sc ON sc.code = cs.service_code
                WHERE cs.company_id = @CompanyId AND cs.service_code = @ServiceCode";

            return await conn.QueryFirstOrDefaultAsync<CompanySubscription>(sql, new { CompanyId = companyId, ServiceCode = serviceCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo suscripcion {ServiceCode} empresa {CompanyId}", serviceCode, companyId);
            throw;
        }
    }

    public async Task UpsertSubscriptionAsync(CompanySubscription subscription)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO platform.company_subscriptions
                    (company_id, service_code, is_active, custom_price, billing_frequency, next_billing_date, started_at, notes)
                VALUES
                    (@CompanyId, @ServiceCode, @IsActive, @CustomPrice, @BillingFrequency, @NextBillingDate, NOW(), @Notes)
                ON CONFLICT (company_id, service_code) DO UPDATE SET
                    is_active           = EXCLUDED.is_active,
                    custom_price        = EXCLUDED.custom_price,
                    billing_frequency   = EXCLUDED.billing_frequency,
                    next_billing_date   = EXCLUDED.next_billing_date,
                    notes               = EXCLUDED.notes,
                    cancelled_at        = CASE WHEN EXCLUDED.is_active = FALSE THEN NOW() ELSE NULL END,
                    updated_at          = NOW()";

            await conn.ExecuteAsync(sql, subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting suscripcion {ServiceCode} empresa {CompanyId}", subscription.ServiceCode, subscription.CompanyId);
            throw;
        }
    }

    public async Task CancelSubscriptionAsync(long companyId, string serviceCode)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE platform.company_subscriptions
                SET is_active = FALSE, cancelled_at = NOW(), updated_at = NOW()
                WHERE company_id = @CompanyId AND service_code = @ServiceCode";

            await conn.ExecuteAsync(sql, new { CompanyId = companyId, ServiceCode = serviceCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando suscripcion {ServiceCode} empresa {CompanyId}", serviceCode, companyId);
            throw;
        }
    }

    public async Task<IEnumerable<CompanySubscription>> GetSubscriptionsDueTodayAsync()
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT cs.id AS Id, cs.company_id AS CompanyId, cs.service_code AS ServiceCode,
                       cs.is_active AS IsActive, cs.custom_price AS CustomPrice,
                       cs.billing_frequency AS BillingFrequency, cs.next_billing_date AS NextBillingDate,
                       cs.started_at AS StartedAt, cs.notes AS Notes,
                       sc.name AS ServiceName, sc.base_price AS BasePrice
                FROM platform.company_subscriptions cs
                INNER JOIN platform.service_catalog sc ON sc.code = cs.service_code
                WHERE cs.is_active = TRUE
                  AND cs.next_billing_date <= CURRENT_DATE";

            return await conn.QueryAsync<CompanySubscription>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo suscripciones vencidas hoy");
            throw;
        }
    }

    public async Task<IEnumerable<(long CompanyId, string CompanyName)>> GetAllCompaniesForBillingAsync()
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS CompanyId, name AS CompanyName
                FROM core.companies
                WHERE is_active = TRUE AND deleted_at IS NULL
                ORDER BY name ASC";

            var results = await conn.QueryAsync<(long CompanyId, string CompanyName)>(sql);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo empresas para billing");
            throw;
        }
    }
}
