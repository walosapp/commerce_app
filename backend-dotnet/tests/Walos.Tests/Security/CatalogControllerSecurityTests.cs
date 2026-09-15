using Microsoft.AspNetCore.Authorization;
using Walos.API.Controllers;

namespace Walos.Tests.Security;

public class CatalogControllerSecurityTests
{
    [Theory]
    [InlineData("CreateCategory", Walos.Application.Security.WalosPolicies.CatalogWrite)]
    [InlineData("UpdateCategory", Walos.Application.Security.WalosPolicies.CatalogWrite)]
    [InlineData("SetCategoryStatus", Walos.Application.Security.WalosPolicies.CatalogWrite)]
    [InlineData("DeleteCategory", Walos.Application.Security.WalosPolicies.CatalogDelete)]
    [InlineData("CreateUnit", Walos.Application.Security.WalosPolicies.CatalogWrite)]
    [InlineData("UpdateUnit", Walos.Application.Security.WalosPolicies.CatalogWrite)]
    [InlineData("SetUnitStatus", Walos.Application.Security.WalosPolicies.CatalogWrite)]
    [InlineData("DeleteUnit", Walos.Application.Security.WalosPolicies.CatalogDelete)]
    public void Catalog_Write_Endpoints_Should_Use_Safe_Centralized_Policy(string methodName, string expectedPolicy)
    {
        var method = typeof(CatalogController).GetMethod(methodName);

        Assert.NotNull(method);

        var authorize = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
        Assert.Null(authorize.Roles);
    }
}
