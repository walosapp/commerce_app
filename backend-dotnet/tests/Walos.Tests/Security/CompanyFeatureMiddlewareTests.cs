using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using Walos.API.Authorization;
using Walos.API.Middleware;
using Walos.API.Services;
using Walos.Application.Services;
using Walos.Domain.Features;

namespace Walos.Tests.Security;

public class CompanyFeatureMiddlewareTests
{
    [Fact]
    public async Task Disabled_Feature_Returns_Distinct_403_And_Does_Not_Run_Action()
    {
        var (context, tenant, service, next) = Context(new RequireFeatureAttribute(WalosFeatures.Finance));
        SetupStates(service, tenant.CompanyId, (WalosFeatures.Finance, false));

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        Assert.False(next.Invocations.Any());
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("feature_not_enabled", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(WalosFeatures.Finance,
            json.RootElement.GetProperty("details").GetProperty("feature").GetString());
    }

    [Fact]
    public async Task Any_Feature_Allows_When_At_Least_One_Is_Enabled()
    {
        var metadata = new RequireAnyFeatureAttribute(WalosFeatures.Restaurant, WalosFeatures.Pos);
        var (context, tenant, service, next) = Context(metadata);
        SetupStates(service, tenant.CompanyId,
            (WalosFeatures.Restaurant, false), (WalosFeatures.Pos, true));

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        next.Verify(n => n(context), Times.Once);
    }

    [Fact]
    public async Task Multiple_Feature_Requirements_Are_All_Required()
    {
        var (context, tenant, service, next) = Context(
            new RequireFeatureAttribute(WalosFeatures.Inventory),
            new RequireFeatureAttribute(WalosFeatures.Ai));
        SetupStates(service, tenant.CompanyId,
            (WalosFeatures.Inventory, true), (WalosFeatures.Ai, false));

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(next.Invocations.Any());
    }

    [Fact]
    public async Task Trusted_Dev_Bypasses_Feature_Gates_Without_Querying_Tenant_Flags()
    {
        var (context, tenant, service, next) = Context(new RequireFeatureAttribute(WalosFeatures.Ai));
        tenant.Role = "dev";
        tenant.IsPlatformAdmin = true;

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        next.Verify(n => n(context), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Platform_Admin_Does_Not_Bypass_Tenant_Feature_Gates()
    {
        var (context, tenant, service, next) = Context(new RequireFeatureAttribute(WalosFeatures.Ai));
        tenant.Role = "platform_admin";
        tenant.IsPlatformAdmin = true;
        SetupStates(service, tenant.CompanyId, (WalosFeatures.Ai, false));

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(next.Invocations.Any());
    }

    [Fact]
    public async Task Repository_Failure_Is_Fail_Closed_And_Does_Not_Run_Action()
    {
        var (context, tenant, service, next) = Context(new RequireFeatureAttribute(WalosFeatures.Ai));
        service.Setup(s => s.GetFeatureStatesAsync(tenant.CompanyId,
                It.IsAny<IReadOnlyCollection<string>>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object));

        Assert.False(next.Invocations.Any());
    }

    [Fact]
    public async Task All_Metadata_Is_Resolved_With_One_Batch_Call()
    {
        var (context, tenant, service, next) = Context(
            new RequireFeatureAttribute(WalosFeatures.Inventory),
            new RequireAnyFeatureAttribute(WalosFeatures.Restaurant, WalosFeatures.Pos));
        SetupStates(service, tenant.CompanyId,
            (WalosFeatures.Inventory, true),
            (WalosFeatures.Restaurant, false),
            (WalosFeatures.Pos, true));

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        next.Verify(n => n(context), Times.Once);
        service.Verify(s => s.GetFeatureStatesAsync(tenant.CompanyId,
            It.Is<IReadOnlyCollection<string>>(codes => codes.Count == 3)), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Missing_Feature_Row_Denies_Fail_Closed()
    {
        var (context, tenant, service, next) = Context(
            new RequireFeatureAttribute(WalosFeatures.Finance));
        SetupStates(service, tenant.CompanyId);

        await new CompanyFeatureMiddleware(next.Object).InvokeAsync(context, tenant, service.Object);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(next.Invocations.Any());
    }

    private static void SetupStates(
        Mock<ICompanyFeatureService> service,
        long companyId,
        params (string Code, bool Enabled)[] states)
    {
        service.Setup(s => s.GetFeatureStatesAsync(companyId,
                It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(states.ToDictionary(x => x.Code, x => x.Enabled, StringComparer.Ordinal));
    }

    private static (DefaultHttpContext Context, TenantContext Tenant,
        Mock<ICompanyFeatureService> Service, Mock<RequestDelegate> Next) Context(params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test"));
        var tenant = new TenantContext { CompanyId = 42, UserId = 5, IsAuthenticated = true };
        var service = new Mock<ICompanyFeatureService>(MockBehavior.Strict);
        var next = new Mock<RequestDelegate>();
        next.Setup(n => n(context)).Returns(Task.CompletedTask);
        return (context, tenant, service, next);
    }
}
