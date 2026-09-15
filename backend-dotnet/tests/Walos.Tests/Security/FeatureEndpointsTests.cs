using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Features;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Features;

namespace Walos.Tests.Security;

public class FeatureEndpointsTests
{
    [Fact]
    public async Task Current_Features_Uses_Authenticated_Tenant_Company()
    {
        var service = new Mock<ICompanyFeatureService>();
        var expected = new List<CompanyFeatureResponse>
        {
            new("dashboard", "Dashboard", null, true, true, 0, null, null)
        };
        service.Setup(s => s.GetCompanyFeaturesAsync(42)).ReturnsAsync(expected);
        var controller = new FeaturesController(service.Object,
            new TenantContext { CompanyId = 42, UserId = 7, IsAuthenticated = true });

        var action = await controller.GetCurrentCompanyFeatures();

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<CompanyFeatureResponse>>>(ok.Value);
        Assert.Same(expected, response.Data);
    }

    [Fact]
    public async Task Platform_Update_Uses_Actor_From_Trusted_Tenant_Context()
    {
        var service = new Mock<ICompanyFeatureService>();
        var controller = new PlatformFeaturesController(service.Object,
            new TenantContext { CompanyId = 1, UserId = 77, IsAuthenticated = true, IsPlatformAdmin = true });

        var action = await controller.SetCompanyFeature(
            42, "finance", new UpdateCompanyFeatureRequest(false));

        Assert.IsType<OkObjectResult>(action);
        service.Verify(s => s.SetCompanyFeatureAsync(42, "finance", false, 77), Times.Once);
    }

    [Fact]
    public async Task Platform_Update_Rejects_Missing_IsEnabled_Without_Service_Call()
    {
        var service = new Mock<ICompanyFeatureService>(MockBehavior.Strict);
        var controller = new PlatformFeaturesController(service.Object,
            new TenantContext { CompanyId = 1, UserId = 77, IsAuthenticated = true, IsPlatformAdmin = true });

        var action = await controller.SetCompanyFeature(
            42, WalosFeatures.Finance, new UpdateCompanyFeatureRequest(null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(action);
        var response = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal("validation_error", response.Code);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Platform_Update_Returns_Stable_Code_When_Disabling_Mandatory_Dashboard()
    {
        var service = new Mock<ICompanyFeatureService>();
        var controller = new PlatformFeaturesController(service.Object,
            new TenantContext { CompanyId = 1, UserId = 77, IsAuthenticated = true, IsPlatformAdmin = true });

        var action = await controller.SetCompanyFeature(
            42, WalosFeatures.Dashboard, new UpdateCompanyFeatureRequest(false));

        var badRequest = Assert.IsType<BadRequestObjectResult>(action);
        var response = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal("feature_always_enabled", response.Code);
        service.Verify(s => s.SetCompanyFeatureAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public void Platform_Feature_Endpoints_Require_Platform_Admin_Policy()
    {
        var authorize = typeof(PlatformFeaturesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(WalosPolicies.PlatformAdmin, authorize.Policy);
    }

    [Fact]
    public void Current_Features_Requires_Authentication_But_Not_Tenant_Manager()
    {
        var authorize = typeof(FeaturesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(authorize.Policy);
        Assert.Null(authorize.Roles);
    }
}
