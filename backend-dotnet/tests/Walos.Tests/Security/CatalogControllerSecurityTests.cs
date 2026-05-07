using Microsoft.AspNetCore.Authorization;
using Walos.API.Controllers;

namespace Walos.Tests.Security;

public class CatalogControllerSecurityTests
{
    [Theory]
    [InlineData("CreateCategory", "dev,super_admin,admin,manager")]
    [InlineData("UpdateCategory", "dev,super_admin,admin,manager")]
    [InlineData("SetCategoryStatus", "dev,super_admin,admin,manager")]
    [InlineData("DeleteCategory", "dev,super_admin,admin")]
    [InlineData("CreateUnit", "dev,super_admin,admin,manager")]
    [InlineData("UpdateUnit", "dev,super_admin,admin,manager")]
    [InlineData("SetUnitStatus", "dev,super_admin,admin,manager")]
    [InlineData("DeleteUnit", "dev,super_admin,admin")]
    public void Catalog_Write_Endpoints_Should_Allow_Expected_Roles(string methodName, string expectedRoles)
    {
        var method = typeof(CatalogController).GetMethod(methodName);

        Assert.NotNull(method);

        var authorize = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(expectedRoles, authorize!.Roles);
    }
}
