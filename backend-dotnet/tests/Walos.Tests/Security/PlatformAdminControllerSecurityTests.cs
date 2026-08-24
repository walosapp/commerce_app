using Microsoft.AspNetCore.Authorization;
using Walos.API.Controllers;
using Walos.Application.Security;

namespace Walos.Tests.Security;

public class PlatformAdminControllerSecurityTests
{
    [Fact]
    public void PlatformAdminController_Should_Require_Dev_Role()
    {
        var authorize = typeof(PlatformAdminController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(WalosPolicies.PlatformAdmin, authorize!.Policy);
    }
}
