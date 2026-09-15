using Microsoft.Extensions.Configuration;
using Npgsql;
using Dapper;
using Walos.Domain.Features;
using Walos.Infrastructure.Repositories;

namespace Walos.Tests.Integration;

[Trait("Category", "PostgreSQL")]
public class CompanyFeatureMigrationPrerequisiteTests
{
    [SkippableFact]
    public async Task Migration019_IsAvailable()
    {
        var connectionString = GetConnectionString();
        Skip.If(string.IsNullOrWhiteSpace(connectionString),
            "Integration tests skipped: No test database connection configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass('platform.features') IS NOT NULL " +
            "AND to_regclass('platform.company_features') IS NOT NULL",
            connection);

        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        Assert.True(exists,
            "Integration gate failed: migration 019_company_features.sql is not applied.");
    }

    private static string? GetConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("TestConnection")
            ?? Environment.GetEnvironmentVariable("WALOS_TEST_CONNECTION");
    }
}

[Trait("Category", "PostgreSQL")]
public class CompanyFeatureRepositoryIntegrationTests : V1IntegrationTestBase
{
    private readonly CompanyFeatureRepository _repository;

    public CompanyFeatureRepositoryIntegrationTests()
    {
        _repository = new CompanyFeatureRepository(ConnectionFactory);
    }

    [SkippableFact]
    public async Task Toggle_IsEnabledAndTenantIsolated()
    {
        await RequireMigration019Async();
        var companyA = await SeedCompanyAsync("Feature tenant A");
        var companyB = await SeedCompanyAsync("Feature tenant B");
        var actorA = await SeedUserAsync(companyA, null);

        Assert.True(await _repository.IsFeatureEnabledAsync(companyA, WalosFeatures.Finance));
        Assert.True(await _repository.IsFeatureEnabledAsync(companyB, WalosFeatures.Finance));

        await _repository.SetCompanyFeatureAsync(companyA, WalosFeatures.Finance, false, actorA);

        Assert.False(await _repository.IsFeatureEnabledAsync(companyA, WalosFeatures.Finance));
        Assert.True(await _repository.IsFeatureEnabledAsync(companyB, WalosFeatures.Finance));

        await _repository.SetCompanyFeatureAsync(companyA, WalosFeatures.Finance, true, actorA);

        Assert.True(await _repository.IsFeatureEnabledAsync(companyA, WalosFeatures.Finance));
    }

    [SkippableFact]
    public async Task Dashboard_CannotBeDisabledAtDatabaseLevel()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Mandatory dashboard tenant");
        var actorId = await SeedUserAsync(companyId, null);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Dashboard, false, actorId));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.True(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Dashboard));
    }

    [SkippableFact]
    public async Task NewReservedTenant_InheritsDashboardOnAndAiOff()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Feature defaults tenant");

        var features = await _repository.GetCompanyFeaturesAsync(companyId);

        Assert.True(features.Single(feature => feature.FeatureCode == WalosFeatures.Dashboard).IsEnabled);
        Assert.False(features.Single(feature => feature.FeatureCode == WalosFeatures.Ai).IsEnabled);
        Assert.False(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Ai));
    }

    [SkippableFact]
    public async Task DefaultMaterialization_CreatesOneRowPerActiveFeatureWithAiOff()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Materialized defaults tenant");

        using var connection = await ConnectionFactory.CreateConnectionAsync();
        await using var insert = new NpgsqlCommand(@"
            INSERT INTO platform.company_features
                (company_id, feature_code, is_enabled)
            SELECT @companyId, f.code, f.default_enabled
            FROM platform.features f
            WHERE f.is_active = TRUE
            ON CONFLICT (company_id, feature_code) DO NOTHING",
            (NpgsqlConnection)connection);
        insert.Parameters.AddWithValue("@companyId", companyId);
        await insert.ExecuteNonQueryAsync();

        await using var query = new NpgsqlCommand(@"
            SELECT COUNT(*) AS total,
                   COUNT(*) FILTER (WHERE feature_code = 'ai' AND is_enabled = FALSE) AS ai_off,
                   COUNT(*) FILTER (WHERE feature_code = 'dashboard' AND is_enabled = TRUE) AS dashboard_on
            FROM platform.company_features
            WHERE company_id = @companyId",
            (NpgsqlConnection)connection);
        query.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(10, reader.GetInt64(0));
        Assert.Equal(1, reader.GetInt64(1));
        Assert.Equal(1, reader.GetInt64(2));
    }

    [SkippableFact]
    public async Task Batch_State_Lookup_Is_Tenant_Scoped_And_Omits_Missing_Code()
    {
        await RequireMigration019Async();
        var companyA = await SeedCompanyAsync("Feature batch A");
        var companyB = await SeedCompanyAsync("Feature batch B");
        var actorA = await SeedUserAsync(companyA, null);
        await _repository.SetCompanyFeatureAsync(companyA, WalosFeatures.Finance, false, actorA);

        var statesA = await _repository.GetFeatureStatesAsync(
            companyA, [WalosFeatures.Finance, WalosFeatures.Pos, "missing"]);
        var statesB = await _repository.GetFeatureStatesAsync(
            companyB, [WalosFeatures.Finance]);

        Assert.False(statesA[WalosFeatures.Finance]);
        Assert.True(statesA[WalosFeatures.Pos]);
        Assert.False(statesA.ContainsKey("missing"));
        Assert.True(statesB[WalosFeatures.Finance]);
    }

    [SkippableFact]
    public async Task Cash_Cannot_Be_Disabled_When_Required_And_A_Sales_Module_Is_Enabled()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Cash dependency");
        var actorId = await SeedUserAsync(companyId, null);
        await SetRequireCashAsync(companyId, true);

        var error = await Assert.ThrowsAsync<Walos.Domain.Exceptions.BusinessException>(() =>
            _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Cash, false, actorId));

        Assert.Equal("feature_dependency_conflict", error.Code);
        Assert.True(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Cash));
    }

    [SkippableFact]
    public async Task Cash_Can_Be_Disabled_When_Requirement_Is_Off()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Cash optional");
        var actorId = await SeedUserAsync(companyId, null);
        await SetRequireCashAsync(companyId, false);

        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Cash, false, actorId);

        Assert.False(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Cash));
    }

    [SkippableFact]
    public async Task Cash_Can_Be_Disabled_When_Both_Sales_Modules_Are_Off()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Cash no sales");
        var actorId = await SeedUserAsync(companyId, null);
        await SetRequireCashAsync(companyId, true);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Restaurant, false, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Pos, false, actorId);

        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Cash, false, actorId);

        Assert.False(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Cash));
    }

    [SkippableTheory]
    [InlineData(WalosFeatures.Restaurant)]
    [InlineData(WalosFeatures.Pos)]
    public async Task Sales_Module_Cannot_Be_Enabled_When_Cash_Is_Required_But_Disabled(
        string salesFeature)
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync($"Cash reverse dependency {salesFeature}");
        var actorId = await SeedUserAsync(companyId, null);
        await SetRequireCashAsync(companyId, true);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Restaurant, false, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Pos, false, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Cash, false, actorId);

        var error = await Assert.ThrowsAsync<Walos.Domain.Exceptions.BusinessException>(() =>
            _repository.SetCompanyFeatureAsync(companyId, salesFeature, true, actorId));

        Assert.Equal("feature_dependency_conflict", error.Code);
        Assert.False(await _repository.IsFeatureEnabledAsync(companyId, salesFeature));
        Assert.False(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Cash));
    }

    [SkippableFact]
    public async Task Enabling_Cash_First_Allows_A_Sales_Module_To_Be_Enabled()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Cash valid transition");
        var actorId = await SeedUserAsync(companyId, null);
        await SetRequireCashAsync(companyId, true);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Restaurant, false, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Pos, false, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Cash, false, actorId);

        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Cash, true, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Restaurant, true, actorId);

        Assert.True(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Cash));
        Assert.True(await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Restaurant));
    }

    [SkippableFact]
    public async Task Concurrent_Opposite_Transitions_Are_Serialized_And_Preserve_Dependency()
    {
        await RequireMigration019Async();
        var companyId = await SeedCompanyAsync("Cash concurrent transition");
        var actorId = await SeedUserAsync(companyId, null);
        await SetRequireCashAsync(companyId, true);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Restaurant, false, actorId);
        await _repository.SetCompanyFeatureAsync(companyId, WalosFeatures.Pos, false, actorId);

        var results = await Task.WhenAll(
            CaptureAsync(() => _repository.SetCompanyFeatureAsync(
                companyId, WalosFeatures.Cash, false, actorId)),
            CaptureAsync(() => _repository.SetCompanyFeatureAsync(
                companyId, WalosFeatures.Restaurant, true, actorId)));

        var conflict = Assert.Single(results.OfType<Walos.Domain.Exceptions.BusinessException>());
        Assert.Equal("feature_dependency_conflict", conflict.Code);
        Assert.Single(results.Where(result => result is null));

        var cashEnabled = await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Cash);
        var restaurantEnabled = await _repository.IsFeatureEnabledAsync(companyId, WalosFeatures.Restaurant);
        Assert.False(restaurantEnabled && !cashEnabled);
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private async Task RequireMigration019Async()
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass('platform.features') IS NOT NULL " +
            "AND to_regclass('platform.company_features') IS NOT NULL",
            (NpgsqlConnection)connection);
        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);

        Assert.True(exists,
            "Integration gate failed: migration 019_company_features.sql is not applied.");
    }

    private async Task SetRequireCashAsync(long companyId, bool required)
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "UPDATE core.companies SET require_cash_register = @Required WHERE id = @CompanyId",
            new { CompanyId = companyId, Required = required });
    }
}
