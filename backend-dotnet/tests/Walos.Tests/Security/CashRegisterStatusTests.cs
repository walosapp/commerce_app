using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.API.Authorization;
using Walos.API.Controllers;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Security;

public class CashRegisterStatusTests
{
    [Theory]
    [InlineData(true, "open")]
    [InlineData(false, "closed")]
    public async Task Status_Is_Branch_Scoped_And_Has_No_Financial_Fields(bool open, string expected)
    {
        var repository = new Mock<ICashRegisterRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetActiveAsync(155, 91)).ReturnsAsync(open ? new CashRegister
        {
            Id = 81, CompanyId = 155, BranchId = 91, Status = "open",
            OpeningAmount = 10000, TotalSales = 50000, Notes = "Private",
        } : null);
        var service = new CashRegisterService(repository.Object, new Mock<IOrderPaymentRepository>().Object,
            NullLogger<CashRegisterService>.Instance);

        var status = await service.GetStatusAsync(155, 91);

        Assert.Equal(new CashRegisterStatusResponse(91, expected), status);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(status, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(new[] { "branchId", "status" }, json.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        repository.VerifyAll();
    }

    [Fact]
    public async Task Controller_Uses_Authenticated_Context_And_Minimal_Response()
    {
        var service = new Mock<ICashRegisterService>(MockBehavior.Strict);
        service.Setup(s => s.GetStatusAsync(155, 91)).ReturnsAsync(new CashRegisterStatusResponse(91, "open"));
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.CompanyId).Returns(155);
        tenant.SetupGet(t => t.BranchId).Returns(91);
        var controller = new CashRegisterStatusController(service.Object, tenant.Object);

        var result = Assert.IsType<OkObjectResult>(await controller.GetStatus());
        var response = Assert.IsType<ApiResponse<CashRegisterStatusResponse>>(result.Value);
        Assert.Equal(new CashRegisterStatusResponse(91, "open"), response.Data);
        service.VerifyAll();
    }

    [Fact]
    public async Task Missing_Branch_Does_Not_Select_An_Arbitrary_Register()
    {
        var service = new Mock<ICashRegisterService>(MockBehavior.Strict);
        var controller = new CashRegisterStatusController(service.Object, new Mock<ITenantContext>().Object);
        Assert.IsType<BadRequestObjectResult>(await controller.GetStatus());
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public void Status_Has_Operational_Features_But_Financial_Controller_Remains_CashOperator()
    {
        var type = typeof(CashRegisterStatusController);
        Assert.Equal(WalosPolicies.SalesTableOperator, type.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.Equal(WalosFeatures.Cash, type.GetCustomAttribute<RequireFeatureAttribute>()!.Feature);
        Assert.Equal(new[] { WalosFeatures.Restaurant, WalosFeatures.Pos }, type.GetCustomAttribute<RequireAnyFeatureAttribute>()!.Features);
        Assert.Equal(WalosPolicies.CashOperator, typeof(CashRegisterController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.DoesNotContain(typeof(CashRegisterController).GetMethods(), m => m.GetCustomAttributes<AllowAnonymousAttribute>().Any());
        Assert.Equal("api/v1/sales/cash-register/status", type.GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Theory]
    [InlineData(WalosRoles.Waiter, false, true, false)]
    [InlineData(WalosRoles.Cashier, false, true, true)]
    [InlineData(WalosRoles.Manager, false, true, true)]
    [InlineData(WalosRoles.SuperAdmin, false, true, true)]
    [InlineData(WalosRoles.Dev, true, true, true)]
    [InlineData(WalosRoles.Dev, false, false, false)]
    [InlineData(WalosRoles.PlatformAdmin, true, false, false)]
    [InlineData("unknown", false, false, false)]
    public async Task Status_Does_Not_Grant_Cash_Operator_Permission(string role, bool platform, bool canRead, bool canManage)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWalosAuthorization();
        using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role),
            new Claim(WalosClaimTypes.PlatformAdmin, platform ? "true" : "false"),
        }, "test"));
        Assert.Equal(canRead, (await authorization.AuthorizeAsync(principal, null, WalosPolicies.SalesTableOperator)).Succeeded);
        Assert.Equal(canManage, (await authorization.AuthorizeAsync(principal, null, WalosPolicies.CashOperator)).Succeeded);
    }

    [Fact]
    public async Task Anonymous_Cannot_Read_Status()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWalosAuthorization();
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity()), null, WalosPolicies.SalesTableOperator);
        Assert.False(result.Succeeded);
    }
}
