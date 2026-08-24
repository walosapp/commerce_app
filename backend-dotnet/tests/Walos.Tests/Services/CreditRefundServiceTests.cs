using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class CreditRefundServiceTests
{
    [Fact]
    public async Task CreditPayment_Rejects_Unsupported_Method()
    {
        var repository = new Mock<ICreditRepository>();
        var service = new CreditService(repository.Object, NullLogger<CreditService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => service.AddPaymentAsync(
            1, 2, 3, 4,
            new AddCreditPaymentRequest { Amount = 10m, PaymentMethod = "crypto" }));

        repository.Verify(r => r.ProcessPaymentAsync(It.IsAny<CreditPaymentCommand>()), Times.Never);
    }

    [Fact]
    public async Task CreditPayment_Normalizes_Admitted_Method()
    {
        var repository = new Mock<ICreditRepository>();
        repository.Setup(r => r.ProcessPaymentAsync(It.IsAny<CreditPaymentCommand>()))
            .ReturnsAsync(new Credit { Id = 1, CompanyId = 2, CreditAmount = 90m });
        repository.Setup(r => r.GetCreditByIdAsync(1, 2, 3)).ReturnsAsync((Credit?)null);
        var service = new CreditService(repository.Object, NullLogger<CreditService>.Instance);

        await service.AddPaymentAsync(1, 2, 3, 4,
            new AddCreditPaymentRequest { Amount = 10m, PaymentMethod = " TRANSFER " });

        repository.Verify(r => r.ProcessPaymentAsync(It.Is<CreditPaymentCommand>(c =>
            c.PaymentMethod == "transfer" && c.BranchId == 3)), Times.Once);
    }

    [Fact]
    public async Task Refund_Requires_Idempotency_Key()
    {
        var repository = new Mock<IRefundRepository>();
        var service = new RefundService(repository.Object, NullLogger<RefundService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRefundAsync(
            1, 2, 3, "", new CreateRefundRequest
            {
                OrderId = 4,
                RefundType = "full",
                Reason = "Motivo suficientemente largo"
            }));

        repository.Verify(r => r.ProcessAsync(It.IsAny<RefundProcessCommand>()), Times.Never);
    }

    [Fact]
    public async Task Refund_Rejects_Duplicate_Order_Item_In_Request()
    {
        var repository = new Mock<IRefundRepository>();
        var service = new RefundService(repository.Object, NullLogger<RefundService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRefundAsync(
            1, 2, 3, "refund-key-123", new CreateRefundRequest
            {
                OrderId = 4,
                RefundType = "partial",
                Reason = "Motivo suficientemente largo",
                Items =
                [
                    new RefundItemRequest { OrderItemId = 5, Quantity = 1 },
                    new RefundItemRequest { OrderItemId = 5, Quantity = 1 }
                ]
            }));

        repository.Verify(r => r.ProcessAsync(It.IsAny<RefundProcessCommand>()), Times.Never);
    }

    [Fact]
    public async Task Refund_Fingerprint_Is_Deterministic_For_Item_Order()
    {
        var repository = new Mock<IRefundRepository>();
        var fingerprints = new List<string>();
        repository.Setup(r => r.ProcessAsync(It.IsAny<RefundProcessCommand>()))
            .Callback<RefundProcessCommand>(c => fingerprints.Add(c.RequestFingerprint))
            .ReturnsAsync((RefundProcessCommand c) => new RefundProcessResult(
                new Refund
                {
                    Id = fingerprints.Count,
                    CompanyId = c.CompanyId,
                    BranchId = c.BranchId,
                    OrderId = c.OrderId,
                    RefundType = c.RefundType,
                    Reason = c.Reason
                }, [], false));
        var service = new RefundService(repository.Object, NullLogger<RefundService>.Instance);

        var requestA = CreatePartialRequest([new() { OrderItemId = 9, Quantity = 1 }, new() { OrderItemId = 8, Quantity = 2 }]);
        var requestB = CreatePartialRequest([new() { OrderItemId = 8, Quantity = 2 }, new() { OrderItemId = 9, Quantity = 1 }]);
        await service.CreateRefundAsync(1, 2, 3, "refund-key-a", requestA);
        await service.CreateRefundAsync(1, 2, 3, "refund-key-b", requestB);

        Assert.Equal(fingerprints[0], fingerprints[1]);
    }

    private static CreateRefundRequest CreatePartialRequest(List<RefundItemRequest> items) => new()
    {
        OrderId = 4,
        RefundType = "partial",
        Reason = "Motivo suficientemente largo",
        Items = items
    };
}
