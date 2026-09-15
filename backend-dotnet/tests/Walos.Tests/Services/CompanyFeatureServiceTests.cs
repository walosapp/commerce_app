using Moq;
using Walos.Application.Services;
using Walos.Domain.Entities.Platform;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class CompanyFeatureServiceTests
{
    private const long CompanyId = 42;
    private const long UserId = 7;

    private readonly Mock<ICompanyFeatureRepository> _repository = new();

    [Fact]
    public void WalosFeatures_DefinesTheExactV1Catalog()
    {
        string[] actual =
        [
            WalosFeatures.Dashboard,
            WalosFeatures.Inventory,
            WalosFeatures.Restaurant,
            WalosFeatures.Pos,
            WalosFeatures.Cash,
            WalosFeatures.Purchases,
            WalosFeatures.Suppliers,
            WalosFeatures.Delivery,
            WalosFeatures.Finance,
            WalosFeatures.Ai
        ];

        Assert.Equal(
            ["dashboard", "inventory", "restaurant", "pos", "cash", "purchases", "suppliers", "delivery", "finance", "ai"],
            actual);
    }

    [Fact]
    public async Task GetFeatureCatalog_MapsRepositoryResult()
    {
        _repository.Setup(repository => repository.GetFeatureCatalogAsync())
            .ReturnsAsync([
                new FeatureDefinition
                {
                    Code = WalosFeatures.Dashboard,
                    Name = "Dashboard",
                    DefaultEnabled = true,
                    IsMandatory = true,
                    IsActive = true,
                    DisplayOrder = 1
                }
            ]);
        var service = new CompanyFeatureService(_repository.Object);

        var result = await service.GetFeatureCatalogAsync();

        var feature = Assert.Single(result);
        Assert.Equal(WalosFeatures.Dashboard, feature.Code);
        Assert.True(feature.DefaultEnabled);
        Assert.True(feature.IsMandatory);
    }

    [Fact]
    public async Task GetCompanyFeatures_MapsRepositoryResult()
    {
        _repository.Setup(repository => repository.CompanyExistsAsync(CompanyId)).ReturnsAsync(true);
        _repository.Setup(repository => repository.GetCompanyFeaturesAsync(CompanyId))
            .ReturnsAsync([
                new CompanyFeature
                {
                    CompanyId = CompanyId,
                    FeatureCode = WalosFeatures.Inventory,
                    Name = "Inventario",
                    Description = "Control de inventario",
                    IsEnabled = true,
                    IsMandatory = false,
                    DisplayOrder = 2,
                    UpdatedAt = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc),
                    UpdatedBy = UserId
                }
            ]);
        var service = new CompanyFeatureService(_repository.Object);

        var result = await service.GetCompanyFeaturesAsync(CompanyId);

        var feature = Assert.Single(result);
        Assert.Equal(WalosFeatures.Inventory, feature.Code);
        Assert.Equal("Inventario", feature.Name);
        Assert.True(feature.IsEnabled);
        Assert.False(feature.IsMandatory);
        Assert.Equal(UserId, feature.UpdatedBy);
        Assert.Equal(new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc), feature.UpdatedAt);
    }

    [Fact]
    public async Task GetCompanyFeatures_ThrowsNotFoundForUnknownCompany()
    {
        _repository.Setup(repository => repository.CompanyExistsAsync(CompanyId)).ReturnsAsync(false);
        var service = new CompanyFeatureService(_repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCompanyFeaturesAsync(CompanyId));

        _repository.Verify(repository => repository.GetCompanyFeaturesAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task IsFeatureEnabled_RejectsUnknownFeatureWithoutQueryingDatabase()
    {
        var service = new CompanyFeatureService(_repository.Object);

        var result = await service.IsFeatureEnabledAsync(CompanyId, "not-a-feature");

        Assert.False(result);
        _repository.Verify(repository => repository.IsFeatureEnabledAsync(
            It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task IsFeatureEnabled_NormalizesKnownFeatureCode()
    {
        _repository.Setup(repository => repository.IsFeatureEnabledAsync(CompanyId, WalosFeatures.Pos))
            .ReturnsAsync(true);
        var service = new CompanyFeatureService(_repository.Object);

        var result = await service.IsFeatureEnabledAsync(CompanyId, " POS ");

        Assert.True(result);
        _repository.Verify(repository => repository.IsFeatureEnabledAsync(CompanyId, WalosFeatures.Pos), Times.Once);
    }

    [Fact]
    public async Task GetFeatureStates_NormalizesKnownCodes_AndUsesOneRepositoryCall()
    {
        _repository.Setup(repository => repository.GetFeatureStatesAsync(
                CompanyId, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new Dictionary<string, bool> { [WalosFeatures.Pos] = true });
        var service = new CompanyFeatureService(_repository.Object);

        var result = await service.GetFeatureStatesAsync(
            CompanyId, [" POS ", "unknown", WalosFeatures.Pos]);

        Assert.True(result[WalosFeatures.Pos]);
        _repository.Verify(repository => repository.GetFeatureStatesAsync(
            CompanyId,
            It.Is<IReadOnlyCollection<string>>(codes =>
                codes.Count == 1 && codes.Single() == WalosFeatures.Pos)), Times.Once);
    }

    [Fact]
    public async Task SetCompanyFeature_RejectsDisablingMandatoryDashboard()
    {
        var service = new CompanyFeatureService(_repository.Object);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetCompanyFeatureAsync(CompanyId, WalosFeatures.Dashboard, false, UserId));

        _repository.Verify(repository => repository.SetCompanyFeatureAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task SetCompanyFeature_RejectsUnknownFeature()
    {
        var service = new CompanyFeatureService(_repository.Object);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetCompanyFeatureAsync(CompanyId, "unknown", true, UserId));

        _repository.Verify(repository => repository.SetCompanyFeatureAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task SetCompanyFeature_DelegatesKnownFeatureAndActor()
    {
        _repository.Setup(repository => repository.CompanyExistsAsync(CompanyId)).ReturnsAsync(true);
        var service = new CompanyFeatureService(_repository.Object);

        await service.SetCompanyFeatureAsync(CompanyId, " FINANCE ", false, UserId);

        _repository.Verify(repository => repository.SetCompanyFeatureAsync(
            CompanyId, WalosFeatures.Finance, false, UserId), Times.Once);
    }

    [Fact]
    public async Task SetCompanyFeature_ThrowsNotFoundForUnknownCompany()
    {
        _repository.Setup(repository => repository.CompanyExistsAsync(CompanyId)).ReturnsAsync(false);
        var service = new CompanyFeatureService(_repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SetCompanyFeatureAsync(CompanyId, WalosFeatures.Finance, true, UserId));

        _repository.Verify(repository => repository.SetCompanyFeatureAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>()), Times.Never);
    }
}
