using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CompanyRepository> _logger;

    public CompanyRepository(IDbConnectionFactory connectionFactory, ILogger<CompanyRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<CompanySettings?> GetCompanySettingsAsync(long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT
                    id AS Id,
                    name AS Name,
                    legal_name AS LegalName,
                    display_name AS DisplayName,
                    email AS Email,
                    phone AS Phone,
                    logo_url AS LogoUrl,
                    currency AS Currency,
                    timezone AS Timezone,
                    language AS Language,
                    theme_preference AS ThemePreference,
                    primary_color AS PrimaryColor,
                    manual_discount_enabled AS ManualDiscountEnabled,
                    max_discount_percent AS MaxDiscountPercent,
                    max_discount_amount AS MaxDiscountAmount,
                    discount_requires_override AS DiscountRequiresOverride,
                    discount_override_threshold_percent AS DiscountOverrideThresholdPercent,
                    business_open_time AS BusinessOpenTime,
                    business_close_time AS BusinessCloseTime,
                    is_active AS IsActive,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    updated_by AS UpdatedBy
                FROM core.companies
                WHERE id = @CompanyId AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<CompanySettings>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuracion de empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<CompanyOperationsSettings?> GetCompanyOperationsSettingsAsync(long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT
                    id AS CompanyId,
                    manual_discount_enabled AS ManualDiscountEnabled,
                    max_discount_percent AS MaxDiscountPercent,
                    max_discount_amount AS MaxDiscountAmount,
                    discount_requires_override AS DiscountRequiresOverride,
                    discount_override_threshold_percent AS DiscountOverrideThresholdPercent
                FROM core.companies
                WHERE id = @CompanyId AND deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<CompanyOperationsSettings>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo reglas operativas de empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<CompanySettings> UpdateCompanySettingsAsync(CompanySettings settings)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE core.companies
                SET
                    name = @Name,
                    display_name = @DisplayName,
                    email = @Email,
                    phone = @Phone,
                    theme_preference = @ThemePreference,
                    business_open_time = @BusinessOpenTime,
                    business_close_time = @BusinessCloseTime,
                    updated_at = NOW(),
                    updated_by = @UpdatedBy
                WHERE id = @Id AND deleted_at IS NULL";

            await connection.ExecuteAsync(sql, new
            {
                settings.Id,
                settings.Name,
                settings.DisplayName,
                settings.Email,
                settings.Phone,
                settings.ThemePreference,
                settings.BusinessOpenTime,
                settings.BusinessCloseTime,
                settings.UpdatedBy
            });

            return (await GetCompanySettingsAsync(settings.Id))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando configuracion de empresa {CompanyId}", settings.Id);
            throw;
        }
    }

    public async Task<CompanyOperationsSettings> UpdateCompanyOperationsSettingsAsync(CompanyOperationsSettings settings)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE core.companies
                SET
                    manual_discount_enabled = @ManualDiscountEnabled,
                    max_discount_percent = @MaxDiscountPercent,
                    max_discount_amount = @MaxDiscountAmount,
                    discount_requires_override = @DiscountRequiresOverride,
                    discount_override_threshold_percent = @DiscountOverrideThresholdPercent,
                    updated_at = NOW()
                WHERE id = @CompanyId AND deleted_at IS NULL";

            await connection.ExecuteAsync(sql, settings);

            return (await GetCompanyOperationsSettingsAsync(settings.CompanyId))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando reglas operativas de empresa {CompanyId}", settings.CompanyId);
            throw;
        }
    }

    public async Task UpdateCompanyLogoAsync(long companyId, string logoUrl, long updatedBy)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE core.companies
                SET
                    logo_url = @LogoUrl,
                    updated_at = NOW(),
                    updated_by = @UpdatedBy
                WHERE id = @CompanyId AND deleted_at IS NULL";

            await connection.ExecuteAsync(sql, new { CompanyId = companyId, LogoUrl = logoUrl, UpdatedBy = updatedBy });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando logo de empresa {CompanyId}", companyId);
            throw;
        }
    }
}
