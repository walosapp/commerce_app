using Dapper;
using System.Data;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Entities.Platform;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class CompanyFeatureRepository : ICompanyFeatureRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CompanyFeatureRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> CompanyExistsAsync(long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM core.companies
                WHERE id = @CompanyId
                  AND deleted_at IS NULL
            )";

        return await connection.ExecuteScalarAsync<bool>(sql, new { CompanyId = companyId });
    }

    public async Task<IReadOnlyList<FeatureDefinition>> GetFeatureCatalogAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT
                code AS Code,
                name AS Name,
                description AS Description,
                default_enabled AS DefaultEnabled,
                is_mandatory AS IsMandatory,
                is_active AS IsActive,
                display_order AS DisplayOrder,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy
            FROM platform.features
            ORDER BY display_order, code";

        var features = await connection.QueryAsync<FeatureDefinition>(sql);
        return features.AsList();
    }

    public async Task<IReadOnlyList<CompanyFeature>> GetCompanyFeaturesAsync(long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT
                @CompanyId AS CompanyId,
                f.code AS FeatureCode,
                f.name AS Name,
                f.description AS Description,
                COALESCE(cf.is_enabled, f.default_enabled) AS IsEnabled,
                f.is_mandatory AS IsMandatory,
                f.display_order AS DisplayOrder,
                cf.created_at AS CreatedAt,
                cf.updated_at AS UpdatedAt,
                cf.updated_by AS UpdatedBy
            FROM core.companies c
            CROSS JOIN platform.features f
            LEFT JOIN platform.company_features cf
                ON cf.company_id = c.id
               AND cf.feature_code = f.code
            WHERE c.id = @CompanyId
              AND c.deleted_at IS NULL
              AND f.is_active = TRUE
            ORDER BY f.display_order, f.code";

        var features = await connection.QueryAsync<CompanyFeature>(sql, new { CompanyId = companyId });
        return features.AsList();
    }

    public async Task<bool> IsFeatureEnabledAsync(long companyId, string featureCode)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT COALESCE(cf.is_enabled, f.default_enabled)
            FROM core.companies c
            CROSS JOIN platform.features f
            LEFT JOIN platform.company_features cf
                ON cf.company_id = c.id
               AND cf.feature_code = f.code
            WHERE c.id = @CompanyId
              AND c.deleted_at IS NULL
              AND f.code = @FeatureCode
              AND f.is_active = TRUE";

        return await connection.QuerySingleOrDefaultAsync<bool>(sql, new
        {
            CompanyId = companyId,
            FeatureCode = featureCode
        });
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(
        long companyId,
        IReadOnlyCollection<string> featureCodes)
    {
        if (featureCodes.Count == 0)
            return new Dictionary<string, bool>(StringComparer.Ordinal);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT
                f.code AS FeatureCode,
                COALESCE(cf.is_enabled, f.default_enabled) AS IsEnabled
            FROM core.companies c
            CROSS JOIN platform.features f
            LEFT JOIN platform.company_features cf
                ON cf.company_id = c.id
               AND cf.feature_code = f.code
            WHERE c.id = @CompanyId
              AND c.deleted_at IS NULL
              AND f.code = ANY(@FeatureCodes)
              AND f.is_active = TRUE";

        var rows = await connection.QueryAsync<(string FeatureCode, bool IsEnabled)>(sql, new
        {
            CompanyId = companyId,
            FeatureCodes = featureCodes.ToArray()
        });
        return rows.ToDictionary(row => row.FeatureCode, row => row.IsEnabled, StringComparer.Ordinal);
    }

    public async Task SetCompanyFeatureAsync(
        long companyId,
        string featureCode,
        bool isEnabled,
        long updatedBy)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        // The company row is the per-tenant transition lock. Read committed is
        // intentional: after waiting for the row lock, dependency reads must see
        // the feature change committed by the previous transition.
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        const string lockCompanySql = @"
            SELECT require_cash_register
            FROM core.companies
            WHERE id = @CompanyId
              AND deleted_at IS NULL
            FOR UPDATE";

        var requireCashRegister = await connection.QuerySingleOrDefaultAsync<bool?>(
            lockCompanySql, new { CompanyId = companyId }, transaction);

        if (!requireCashRegister.HasValue)
            throw new InvalidOperationException("Company must exist before changing its features");

        if (isEnabled
            && requireCashRegister.Value
            && (featureCode == WalosFeatures.Restaurant || featureCode == WalosFeatures.Pos))
        {
            const string cashFeatureSql = @"
                SELECT COALESCE(cf.is_enabled, f.default_enabled)
                FROM platform.features f
                LEFT JOIN platform.company_features cf
                    ON cf.company_id = @CompanyId
                   AND cf.feature_code = f.code
                WHERE f.code = @CashFeatureCode
                  AND f.is_active = TRUE";

            var cashEnabled = await connection.QuerySingleOrDefaultAsync<bool>(cashFeatureSql, new
            {
                CompanyId = companyId,
                CashFeatureCode = WalosFeatures.Cash
            }, transaction);

            if (!cashEnabled)
            {
                throw new BusinessException(
                    "No se puede habilitar un modulo de ventas mientras caja sea obligatoria y este desactivada",
                    "feature_dependency_conflict");
            }
        }

        if (featureCode == WalosFeatures.Cash && !isEnabled && requireCashRegister.Value)
        {
            const string salesFeatureSql = @"
                SELECT COALESCE(cf.is_enabled, f.default_enabled)
                FROM platform.features f
                LEFT JOIN platform.company_features cf
                    ON cf.company_id = @CompanyId
                   AND cf.feature_code = f.code
                WHERE f.code = ANY(@FeatureCodes)
                  AND f.is_active = TRUE";

            var salesStates = await connection.QueryAsync<bool>(salesFeatureSql, new
            {
                CompanyId = companyId,
                FeatureCodes = new[] { WalosFeatures.Restaurant, WalosFeatures.Pos }
            }, transaction);

            if (salesStates.Any(enabled => enabled))
            {
                throw new BusinessException(
                    "No se puede desactivar caja mientras sea obligatoria para ventas habilitadas",
                    "feature_dependency_conflict");
            }
        }

        const string sql = @"
            INSERT INTO platform.company_features
                (company_id, feature_code, is_enabled, updated_by)
            VALUES
                (@CompanyId, @FeatureCode, @IsEnabled, @UpdatedBy)
            ON CONFLICT (company_id, feature_code) DO UPDATE SET
                is_enabled = EXCLUDED.is_enabled,
                updated_at = NOW(),
                updated_by = EXCLUDED.updated_by";

        await connection.ExecuteAsync(sql, new
        {
            CompanyId = companyId,
            FeatureCode = featureCode,
            IsEnabled = isEnabled,
            UpdatedBy = updatedBy
        }, transaction);
        transaction.Commit();
    }
}
