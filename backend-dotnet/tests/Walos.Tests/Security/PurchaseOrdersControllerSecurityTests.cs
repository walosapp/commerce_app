using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Security;

public class PurchaseOrdersControllerSecurityTests
{
    [Fact]
    public async Task Create_Rejects_Request_Branch_Different_From_Authenticated_Branch()
    {
        var service = new Mock<IPurchaseOrderService>();
        var tenant = new TenantContext
        {
            CompanyId = 7,
            UserId = 11,
            BranchId = 13,
            Role = "manager",
            IsAuthenticated = true
        };
        var request = new CreatePurchaseOrderRequest
        {
            BranchId = 999,
            SupplierId = 17,
            Items = [new PurchaseOrderItemDto { ProductId = 19, Quantity = 1, UnitCost = 10 }]
        };
        var controller = new PurchaseOrdersController(service.Object, tenant);
        await Assert.ThrowsAsync<ValidationException>(() => controller.Create(request));

        service.Verify(candidate => candidate.CreateAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CreatePurchaseOrderRequest>()), Times.Never);
        service.Verify(candidate => candidate.CreateAsync(7, 999, 11, request), Times.Never);
    }

    [Fact]
    public async Task Create_Uses_Authenticated_Branch_When_Request_Matches()
    {
        var service = new Mock<IPurchaseOrderService>();
        var tenant = new TenantContext
        {
            CompanyId = 7,
            UserId = 11,
            BranchId = 13,
            Role = "manager",
            IsAuthenticated = true
        };
        var request = new CreatePurchaseOrderRequest
        {
            BranchId = 13,
            SupplierId = 17,
            Items = [new PurchaseOrderItemDto { ProductId = 19, Quantity = 1, UnitCost = 10 }]
        };
        service.Setup(candidate => candidate.CreateAsync(7, 13, 11, request))
            .ReturnsAsync(new PurchaseOrderResponse { Id = 23, BranchId = 13 });

        var controller = new PurchaseOrdersController(service.Object, tenant);
        await controller.Create(request);

        service.Verify(candidate => candidate.CreateAsync(7, 13, 11, request), Times.Once);
    }
}
