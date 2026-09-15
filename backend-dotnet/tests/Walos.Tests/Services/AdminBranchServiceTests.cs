using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.Application.DTOs.Admin;
using Walos.Application.Services;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Services;

public sealed class AdminBranchServiceTests
{
    private readonly Mock<IAdminRepository> _repository = new(MockBehavior.Strict);

    [Fact]
    public async Task Create_Rejects_Missing_Required_Fields_Without_Repository_Call()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateBranchAsync(
            42, new CreateBranchAdminRequest(), 7));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_Rejects_Explicit_Null_Required_Field_Without_NullReference()
    {
        var service = CreateService();
        var request = ValidCreate();
        request.Name = null!;

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateBranchAsync(42, request, 7));

        Assert.Contains("nombre", error.Message, StringComparison.OrdinalIgnoreCase);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_Rejects_Invalid_Capacity(int capacity)
    {
        var service = CreateService();
        var request = ValidCreate();
        request.MaxCapacity = capacity;

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateBranchAsync(42, request, 7));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_Normalizes_Code_And_Delegates_Tenant_And_Actor()
    {
        var request = ValidCreate();
        request.Code = " norte ";
        var expected = Branch(10, 42, "NORTE", true);
        _repository.Setup(repository => repository.CreateBranchAsync(42, request, 7))
            .ReturnsAsync(expected);
        var service = CreateService();

        var result = await service.CreateBranchAsync(42, request, 7);

        Assert.Same(expected, result);
        Assert.Equal("NORTE", request.Code);
        _repository.VerifyAll();
    }

    [Fact]
    public async Task Update_Maps_Tenant_Scoped_NotFound()
    {
        var request = ValidUpdate(false);
        _repository.Setup(repository => repository.UpdateBranchAsync(42, 10, request, 7))
            .ReturnsAsync((BranchAdminResponse?)null);
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateBranchAsync(42, 10, request, 7));
    }

    [Fact]
    public async Task Update_Rejects_Empty_Request_Without_Repository_Call()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateBranchAsync(
            42, 10, new UpdateBranchAdminRequest(), 7));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_Rejects_Missing_Mandatory_Fields_Without_Repository_Call()
    {
        var request = ValidUpdate();
        request.Address = null;
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateBranchAsync(42, 10, request, 7));

        _repository.VerifyNoOtherCalls();
    }

    private AdminService CreateService() => new(_repository.Object, NullLogger<AdminService>.Instance);

    private static CreateBranchAdminRequest ValidCreate() => new()
    {
        Name = "Norte",
        Code = "NORTE",
        BranchType = "general",
        Address = "Calle 1",
        City = "Bogota",
        Country = "CO",
        MaxTables = 10,
        MaxCapacity = 40
    };

    private static UpdateBranchAdminRequest ValidUpdate(bool isActive = true) => new()
    {
        Name = "Norte",
        Code = "NORTE",
        BranchType = "general",
        Address = "Calle 1",
        City = "Bogota",
        Country = "CO",
        MaxTables = 10,
        MaxCapacity = 40,
        IsActive = isActive
    };

    internal static BranchAdminResponse Branch(long id, long companyId, string code, bool active) =>
        new(id, companyId, "Norte", code, "general", null, null, "Calle 1", "Bogota",
            null, "CO", null, 10, 40, false, active, DateTime.UtcNow, null);
}
