using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class CompanyRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetCompanySettingsAsync_ReturnsSettings_WhenExists()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Settings Co");

        // Act
        var settings = await CompanyRepository.GetCompanySettingsAsync(companyId);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(companyId, settings.Id);
        Assert.Equal("Settings Co", settings.Name);
    }

    [SkippableFact]
    public async Task GetCompanySettingsAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var settings = await CompanyRepository.GetCompanySettingsAsync(999999);

        // Assert
        Assert.Null(settings);
    }

    [SkippableFact]
    public async Task UpdateCompanySettingsAsync_UpdatesFields()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Update Co");
        var settings = await CompanyRepository.GetCompanySettingsAsync(companyId);
        Assert.NotNull(settings);

        settings.DisplayName = "Updated Name";
        settings.Email = "updated@example.com";
        settings.Phone = "555-1234";
        settings.ThemePreference = "dark";

        // Act
        var updated = await CompanyRepository.UpdateCompanySettingsAsync(settings);

        // Assert
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("updated@example.com", updated.Email);
        Assert.Equal("dark", updated.ThemePreference);

        // Verify persistence
        var fromDb = await CompanyRepository.GetCompanySettingsAsync(companyId);
        Assert.Equal("Updated Name", fromDb!.DisplayName);
    }

    [SkippableFact]
    public async Task GetCompanyOperationsSettingsAsync_ReturnsSettings_WhenExists()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Ops Settings Co");

        // Act
        var ops = await CompanyRepository.GetCompanyOperationsSettingsAsync(companyId);

        // Assert
        Assert.NotNull(ops);
        Assert.Equal(companyId, ops.CompanyId);
    }

    [SkippableFact]
    public async Task UpdateCompanyOperationsSettingsAsync_UpdatesRules()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Ops Update Co");
        var ops = new CompanyOperationsSettings
        {
            CompanyId = companyId,
            ManualDiscountEnabled = true,
            MaxDiscountPercent = 25,
            MaxDiscountAmount = 100000,
            DiscountRequiresOverride = true,
            DiscountOverrideThresholdPercent = 15
        };

        // Act
        var updated = await CompanyRepository.UpdateCompanyOperationsSettingsAsync(ops);

        // Assert
        Assert.Equal(25, updated.MaxDiscountPercent);
        Assert.True(updated.ManualDiscountEnabled);
        Assert.True(updated.DiscountRequiresOverride);

        // Verify persistence
        var fromDb = await CompanyRepository.GetCompanyOperationsSettingsAsync(companyId);
        Assert.Equal(25, fromDb!.MaxDiscountPercent);
        Assert.Equal(15, fromDb.DiscountOverrideThresholdPercent);
    }

    [SkippableFact]
    public async Task UpdateCompanyLogoAsync_UpdatesLogoUrl()
    {
        // Arrange
        var companyId = await SeedCompanyAsync("Logo Co");
        var logoUrl = "/uploads/test-logo.png";

        // Act
        await CompanyRepository.UpdateCompanyLogoAsync(companyId, logoUrl, 1);

        // Assert
        var settings = await CompanyRepository.GetCompanySettingsAsync(companyId);
        Assert.Equal(logoUrl, settings!.LogoUrl);
    }

    [SkippableFact]
    public async Task MultiTenant_Isolation_Works()
    {
        // Arrange - create two companies
        var company1 = await SeedCompanyAsync("Company One");
        var company2 = await SeedCompanyAsync("Company Two");

        // Update company 1 settings
        var settings1 = await CompanyRepository.GetCompanySettingsAsync(company1);
        settings1!.DisplayName = "Company One Updated";
        await CompanyRepository.UpdateCompanySettingsAsync(settings1);

        // Act
        var fromDb1 = await CompanyRepository.GetCompanySettingsAsync(company1);
        var fromDb2 = await CompanyRepository.GetCompanySettingsAsync(company2);

        // Assert - each company has its own data
        Assert.Equal("Company One Updated", fromDb1!.DisplayName);
        Assert.Equal("Company Two", fromDb2!.Name); // Company 2 unchanged
    }
}
