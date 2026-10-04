using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class CashRegisterServiceTests
{
    private const long CompanyId = 10;
    private const long BranchId = 20;
    private const long OpeningUserId = 30;
    private const long OtherCashierId = 40;

    [Fact]
    public async Task Active_Register_Is_Shared_By_Authorized_Users_In_The_Same_Branch()
    {
        var repository = new Mock<ICashRegisterRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetActiveAsync(CompanyId, BranchId))
            .ReturnsAsync(OpenRegister());
        var service = CreateService(repository);

        var active = await service.GetActiveAsync(CompanyId, BranchId);

        Assert.NotNull(active);
        Assert.Equal(OpeningUserId, active.OpenedBy);
        Assert.Equal(BranchId, active.BranchId);
        repository.VerifyAll();
    }

    [Fact]
    public async Task Open_Delegates_Duplicate_Detection_To_Atomic_Repository_Operation()
    {
        var repository = new Mock<ICashRegisterRepository>(MockBehavior.Strict);
        repository.Setup(x => x.OpenAsync(It.Is<CashRegister>(register =>
                register.CompanyId == CompanyId
                && register.BranchId == BranchId
                && register.OpenedBy == OtherCashierId)))
            .ThrowsAsync(new BusinessException(
                "Ya existe una caja abierta en esta sucursal.",
                "cash_register_already_open"));
        var service = CreateService(repository);

        var error = await Assert.ThrowsAsync<BusinessException>(() => service.OpenAsync(
            CompanyId,
            BranchId,
            OtherCashierId,
            new OpenCashRegisterRequest(100m, null)));

        Assert.Equal("cash_register_already_open", error.Code);
        repository.VerifyAll();
    }

    [Fact]
    public async Task Open_Preserves_Branch_And_Opening_User_Traceability()
    {
        var repository = new Mock<ICashRegisterRepository>(MockBehavior.Strict);
        repository.Setup(x => x.OpenAsync(It.Is<CashRegister>(register =>
                register.CompanyId == CompanyId
                && register.BranchId == BranchId
                && register.OpenedBy == OtherCashierId
                && register.OpeningAmount == 75m)))
            .ReturnsAsync(new CashRegister
            {
                Id = 60,
                CompanyId = CompanyId,
                BranchId = BranchId,
                OpenedBy = OtherCashierId,
                Status = "open",
                OpeningAmount = 75m,
                OpenedAt = DateTime.UtcNow
            });
        var service = CreateService(repository);

        var opened = await service.OpenAsync(
            CompanyId,
            BranchId,
            OtherCashierId,
            new OpenCashRegisterRequest(75m, null));

        Assert.Equal(BranchId, opened.BranchId);
        Assert.Equal(OtherCashierId, opened.OpenedBy);
        repository.VerifyAll();
    }

    [Fact]
    public async Task Active_Register_From_Another_Branch_Is_Not_Returned()
    {
        const long anotherBranchId = 21;
        var repository = new Mock<ICashRegisterRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetActiveAsync(CompanyId, anotherBranchId))
            .ReturnsAsync((CashRegister?)null);
        var service = CreateService(repository);

        var active = await service.GetActiveAsync(CompanyId, anotherBranchId);

        Assert.Null(active);
        repository.VerifyAll();
    }

    [Fact]
    public async Task Authorized_User_Can_Close_Register_Opened_By_Another_User()
    {
        const long registerId = 50;
        var repository = new Mock<ICashRegisterRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetByIdAsync(registerId, CompanyId, BranchId))
            .ReturnsAsync(OpenRegister(registerId));
        repository.Setup(x => x.CloseAsync(
                registerId, CompanyId, BranchId, OtherCashierId, 125m, "Turno terminado"))
            .ReturnsAsync(new CashRegister
            {
                Id = registerId,
                CompanyId = CompanyId,
                BranchId = BranchId,
                OpenedBy = OpeningUserId,
                ClosedBy = OtherCashierId,
                Status = "closed",
                OpeningAmount = 100m,
                ClosingAmount = 125m,
                OpenedAt = DateTime.UtcNow.AddHours(-1),
                ClosedAt = DateTime.UtcNow
            });
        var service = CreateService(repository);

        var closed = await service.CloseAsync(
            registerId,
            CompanyId,
            BranchId,
            OtherCashierId,
            new CloseCashRegisterRequest(125m, "Turno terminado"));

        Assert.Equal(OpeningUserId, closed.OpenedBy);
        Assert.Equal(OtherCashierId, closed.ClosedBy);
        Assert.Equal("closed", closed.Status);
        repository.VerifyAll();
    }

    private static CashRegisterService CreateService(Mock<ICashRegisterRepository> repository) =>
        new(
            repository.Object,
            Mock.Of<IOrderPaymentRepository>(),
            NullLogger<CashRegisterService>.Instance);

    private static CashRegister OpenRegister(long id = 50) => new()
    {
        Id = id,
        CompanyId = CompanyId,
        BranchId = BranchId,
        OpenedBy = OpeningUserId,
        Status = "open",
        OpeningAmount = 100m,
        OpenedAt = DateTime.UtcNow.AddMinutes(-10)
    };
}
