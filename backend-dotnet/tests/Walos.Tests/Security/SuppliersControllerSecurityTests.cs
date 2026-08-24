using Moq;
using Walos.API.Controllers;
using Walos.API.Services;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Security;

public class SuppliersControllerSecurityTests
{
    [Fact]
    public async Task Create_Rejects_Request_Branch_Different_From_Authenticated_Branch()
    {
        var service = new Mock<ISuppliersService>();
        var tenant = AuthenticatedTenant(branchId: 13);
        var request = new CreateSupplierRequest { BranchId = 99, Name = "Proveedor" };
        var controller = new SuppliersController(service.Object, tenant);

        await Assert.ThrowsAsync<ValidationException>(() => controller.Create(request));

        service.Verify(candidate => candidate.CreateAsync(
            It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long>(), It.IsAny<CreateSupplierRequest>()), Times.Never);
    }

    [Fact]
    public async Task Create_Uses_Authenticated_Branch_When_Request_Omits_It()
    {
        var service = new Mock<ISuppliersService>();
        var tenant = AuthenticatedTenant(branchId: 13);
        var request = new CreateSupplierRequest { Name = "Proveedor" };
        service.Setup(candidate => candidate.CreateAsync(7, 13, 11, request))
            .ReturnsAsync(new Supplier { Id = 23, CompanyId = 7, BranchId = 13, Name = request.Name });
        var controller = new SuppliersController(service.Object, tenant);

        await controller.Create(request);

        service.Verify(candidate => candidate.CreateAsync(7, 13, 11, request), Times.Once);
    }

    [Fact]
    public async Task Create_Uses_Selected_Branch_For_CompanyWide_User()
    {
        var service = new Mock<ISuppliersService>();
        var tenant = AuthenticatedTenant(branchId: null);
        var request = new CreateSupplierRequest { BranchId = 17, Name = "Proveedor" };
        service.Setup(candidate => candidate.CreateAsync(7, 17, 11, request))
            .ReturnsAsync(new Supplier { Id = 23, CompanyId = 7, BranchId = 17, Name = request.Name });
        var controller = new SuppliersController(service.Object, tenant);

        await controller.Create(request);

        service.Verify(candidate => candidate.CreateAsync(7, 17, 11, request), Times.Once);
    }

    [Fact]
    public async Task Detail_And_Mutations_Propagate_Authenticated_Branch()
    {
        var service = new Mock<ISuppliersService>();
        var tenant = AuthenticatedTenant(branchId: 13);
        var update = new UpdateSupplierRequest { Name = "Actualizado" };
        var association = new AddSupplierProductRequest { ProductId = 31 };
        var controller = new SuppliersController(service.Object, tenant);

        await controller.GetById(19);
        await controller.Update(19, update);
        await controller.Delete(19);
        service.Setup(candidate => candidate.AddProductAsync(7, 13, 19, association))
            .ReturnsAsync(new SupplierProduct { SupplierId = 19, ProductId = 31 });
        await controller.AddProduct(19, association);
        await controller.RemoveProduct(19, 31);

        service.Verify(candidate => candidate.GetByIdAsync(19, 7, 13), Times.Once);
        service.Verify(candidate => candidate.UpdateAsync(7, 13, 19, update), Times.Once);
        service.Verify(candidate => candidate.DeleteAsync(19, 7, 13), Times.Once);
        service.Verify(candidate => candidate.AddProductAsync(7, 13, 19, association), Times.Once);
        service.Verify(candidate => candidate.RemoveProductAsync(7, 13, 19, 31), Times.Once);
    }

    private static TenantContext AuthenticatedTenant(long? branchId) => new()
    {
        CompanyId = 7,
        UserId = 11,
        BranchId = branchId,
        Role = "manager",
        IsAuthenticated = true,
    };
}
