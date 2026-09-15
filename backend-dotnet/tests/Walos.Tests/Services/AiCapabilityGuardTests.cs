using Moq;
using Walos.Application.Services;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;

namespace Walos.Tests.Services;

public class AiCapabilityGuardTests
{
    [Fact]
    public async Task Missing_Capability_Fails_Closed_With_Stable_Code()
    {
        var features = new Mock<ICompanyFeatureService>();
        features.Setup(service => service.GetFeatureStatesAsync(10, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new Dictionary<string, bool> { [WalosFeatures.Ai] = true });

        var snapshot = await new AiCapabilityGuard(features.Object).GetSnapshotAsync(10);

        var error = Assert.Throws<FeatureNotEnabledException>(() => snapshot.Ensure(WalosFeatures.Inventory));
        Assert.Equal("feature_not_enabled", error.Code);
        Assert.Equal(WalosFeatures.Inventory, error.Feature);
    }

    [Fact]
    public async Task Fingerprint_Is_Deterministic_And_Changes_With_Capabilities()
    {
        var features = new Mock<ICompanyFeatureService>();
        features.SetupSequence(service => service.GetFeatureStatesAsync(10, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new Dictionary<string, bool> { [WalosFeatures.Ai] = true })
            .ReturnsAsync(new Dictionary<string, bool> { [WalosFeatures.Ai] = true })
            .ReturnsAsync(new Dictionary<string, bool> { [WalosFeatures.Ai] = true, [WalosFeatures.Inventory] = true });
        var guard = new AiCapabilityGuard(features.Object);

        var first = await guard.GetSnapshotAsync(10);
        var same = await guard.GetSnapshotAsync(10);
        var changed = await guard.GetSnapshotAsync(10);

        Assert.Equal(first.Fingerprint, same.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
    }

    [Fact]
    public async Task Trusted_Dev_Bypass_Does_Not_Query_Tenant_Features()
    {
        var features = new Mock<ICompanyFeatureService>();
        var snapshot = await new AiCapabilityGuard(features.Object).GetSnapshotAsync(10, trustedDevBypass: true);

        snapshot.Ensure(WalosFeatures.Ai, WalosFeatures.Inventory, WalosFeatures.Purchases,
            WalosFeatures.Suppliers, WalosFeatures.Delivery);
        features.VerifyNoOtherCalls();
    }
}
