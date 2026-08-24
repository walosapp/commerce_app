using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.Services;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Security;

public class DeliveryControllerSecurityTests
{
    [Fact]
    public async Task Branch_Scoped_Endpoint_Rejects_Authenticated_Context_Without_Branch()
    {
        var service = new Mock<IDeliveryService>(MockBehavior.Strict);
        var tenant = new TenantContext
        {
            IsAuthenticated = true,
            CompanyId = 10,
            UserId = 20,
            BranchId = null,
            Role = "manager"
        };
        var controller = new DeliveryController(service.Object, tenant);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.GetOrders(null, null, null));

        service.VerifyNoOtherCalls();
    }
}
