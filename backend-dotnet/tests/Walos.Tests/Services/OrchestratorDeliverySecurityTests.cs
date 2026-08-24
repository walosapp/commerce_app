using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class OrchestratorDeliverySecurityTests
{
    [Fact]
    public async Task Delivery_Update_Status_Intent_Is_Query_Only_And_Never_Calls_Mutation_Repository()
    {
        var sessions = new Mock<IAiSessionRepository>();
        var ai = new Mock<IAiService>();
        var inventory = new Mock<IInventoryRepository>();
        var delivery = new Mock<IDeliveryRepository>();
        var suppliers = new Mock<ISuppliersRepository>();
        sessions.Setup(repository => repository.GetOrCreateSessionAsync(10, 20))
            .ReturnsAsync(new AiSession { Id = 30, CompanyId = 10, UserId = 20, Context = "{}" });
        sessions.Setup(repository => repository.GetMessagesAsync(30, It.IsAny<int>()))
            .ReturnsAsync([]);
        ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("delivery");
        ai.Setup(service => service.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync("{\"action\":\"update_status\",\"response\":\"Actualizado\",\"order_number\":\"DEL-0001\",\"new_status\":\"ready\"}");
        delivery.Setup(repository => repository.GetOrdersAsync(10, 40, null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(
            [
                new DeliveryOrder
                {
                    Id = 50,
                    CompanyId = 10,
                    BranchId = 40,
                    OrderNumber = "DEL-0001",
                    Status = "accepted"
                }
            ]);
        var orchestrator = new OrchestratorService(
            sessions.Object,
            ai.Object,
            inventory.Object,
            delivery.Object,
            suppliers.Object,
            NullLogger<OrchestratorService>.Instance);

        var response = await orchestrator.ChatAsync(10, 20, 40, "Walos", "marca el pedido listo", null);

        Assert.Equal("delivery", response.AgentType);
        Assert.Equal("text", response.ResponseType);
        Assert.Contains("modulo de domicilios", response.Message);
        delivery.Verify(repository => repository.UpdateOrderStatusAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(),
                It.IsAny<Dictionary<string, DateTime?>>()), Times.Never);
    }
}
