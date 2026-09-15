using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.Application.DTOs.Admin;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Services;

public sealed class AdminTenantServiceTests
{
    private const long CompanyId = 42;
    private readonly Mock<IAdminRepository> _repository = new(MockBehavior.Strict);

    [Fact]
    public async Task SystemTenant_CannotBeDeactivated_AndUsesStableCode()
    {
        _repository.Setup(repository => repository.GetTenantByIdAsync(CompanyId))
            .ReturnsAsync(SystemTenant());
        var service = CreateService();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SetTenantActiveAsync(CompanyId, false));

        Assert.Equal("system_tenant_protected", error.Code);
        _repository.Verify(repository => repository.SetTenantActiveAsync(
            It.IsAny<long>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SystemTenant_TaxIdCannotBeChanged_AndUsesStableCode()
    {
        var request = new UpdateTenantRequest { TaxId = "OTHER-TENANT" };
        _repository.Setup(repository => repository.GetTenantByIdAsync(CompanyId))
            .ReturnsAsync(SystemTenant());
        var service = CreateService();

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateTenantAsync(CompanyId, request));

        Assert.Equal("system_tenant_protected", error.Code);
        _repository.Verify(repository => repository.UpdateTenantAsync(
            It.IsAny<long>(), It.IsAny<UpdateTenantRequest>()), Times.Never);
    }

    [Fact]
    public async Task SystemTenant_AllowsHarmlessEditsWithoutChangingIdentity()
    {
        var request = new UpdateTenantRequest
        {
            Name = "Walos Platform",
            TaxId = $" {WalosSystemIdentity.CompanyTaxId} "
        };
        var expected = SystemTenant();
        expected.Name = "Walos Platform";
        _repository.Setup(repository => repository.GetTenantByIdAsync(CompanyId))
            .ReturnsAsync(SystemTenant());
        _repository.Setup(repository => repository.UpdateTenantAsync(CompanyId, request))
            .ReturnsAsync(expected);
        var service = CreateService();

        var result = await service.UpdateTenantAsync(CompanyId, request);

        Assert.Same(expected, result);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task NormalTenant_CanBeDeactivated()
    {
        _repository.Setup(repository => repository.GetTenantByIdAsync(CompanyId))
            .ReturnsAsync(new TenantResponse { Id = CompanyId, IsSystem = false, IsActive = true });
        _repository.Setup(repository => repository.SetTenantActiveAsync(CompanyId, false))
            .ReturnsAsync(true);
        var service = CreateService();

        Assert.True(await service.SetTenantActiveAsync(CompanyId, false));
        _repository.VerifyAll();
    }

    private AdminService CreateService() =>
        new(_repository.Object, NullLogger<AdminService>.Instance);

    private static TenantResponse SystemTenant() => new()
    {
        Id = CompanyId,
        Name = "Walos System",
        TaxId = WalosSystemIdentity.CompanyTaxId,
        IsSystem = true,
        IsActive = true
    };
}
