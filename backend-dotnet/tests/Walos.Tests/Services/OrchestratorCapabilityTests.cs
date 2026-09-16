using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class OrchestratorCapabilityTests
{
    [Fact]
    public async Task Inventory_Disabled_Fails_Before_Repository_And_Does_Not_Persist_Message()
    {
        var fixture = new Fixture(WalosFeatures.Ai);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("inventory");

        var error = await Assert.ThrowsAsync<FeatureNotEnabledException>(() =>
            fixture.Service.ChatAsync(10, 20, 30, "Walos", "consulta stock", null));

        Assert.Equal(WalosFeatures.Inventory, error.Feature);
        fixture.Inventory.Verify(repository => repository.GetAllProductsAsync(
            It.IsAny<long>(), It.IsAny<ProductFilter?>()), Times.Never);
        fixture.Sessions.Verify(repository => repository.AddMessageAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Provided_Session_Is_Looked_Up_By_Company_And_User()
    {
        var fixture = new Fixture(WalosFeatures.Ai);
        fixture.Sessions.Setup(repository => repository.GetSessionAsync(99, 10, 20))
            .ReturnsAsync(fixture.Session);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("general");
        fixture.Ai.Setup(service => service.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync("respuesta");

        await fixture.Service.ChatAsync(10, 20, 30, "Walos", "hola", 99);

        fixture.Sessions.Verify(repository => repository.GetSessionAsync(99, 10, 20), Times.Once);
    }

    [Fact]
    public async Task Capability_Change_Resets_History_And_Pending_Flow_Before_Model_Call()
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Inventory);
        fixture.Session.Context = "{\"capability_fingerprint\":\"old\",\"flow\":\"awaiting_stock_confirmation\",\"pending_stock\":\"secret\"}";
        fixture.Ai.Setup(service => service.ClassifyAsync(It.Is<string>(prompt => !prompt.Contains("secret"))))
            .ReturnsAsync("general");
        fixture.Ai.Setup(service => service.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync("respuesta");

        await fixture.Service.ChatAsync(10, 20, 30, "Walos", "hola", null);

        fixture.Sessions.Verify(repository => repository.ResetSessionAsync(
            fixture.Session.Id, 10, 20,
            It.Is<string>(json => json.Contains("capability_fingerprint") && !json.Contains("pending_stock"))), Times.Once);
    }

    [Fact]
    public async Task Message_From_Concurrent_Stale_Capability_Snapshot_Is_Not_Sent_To_Model()
    {
        var fixture = new Fixture(WalosFeatures.Ai);
        fixture.Sessions.Setup(repository => repository.GetMessagesAsync(fixture.Session.Id, 10, 20, It.IsAny<int>()))
            .ReturnsAsync([
                new AiMessage
                {
                    Role = "user",
                    Content = "disabled module secret",
                    Metadata = "{\"capability_fingerprint\":\"old\"}"
                }
            ]);
        fixture.Ai.Setup(service => service.ClassifyAsync(
                It.Is<string>(prompt => !prompt.Contains("disabled module secret"))))
            .ReturnsAsync("general");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.Is<string>(prompt => !prompt.Contains("disabled module secret")),
                It.IsAny<string>(),
                null))
            .ReturnsAsync("respuesta");

        await fixture.Service.ChatAsync(10, 20, 30, "Walos", "hola", null);

        fixture.Ai.VerifyAll();
    }

    [Theory]
    [InlineData("add_stock")]
    [InlineData("create_and_stock")]
    public async Task Ai_Stock_Mutation_Action_Is_Disabled_Without_Any_Stock_Mutation(
        string action)
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Inventory);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("inventory");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync($"{{\"action\":\"{action}\",\"response\":\"propuesta\",\"data\":{{\"products\":[]}}}}");
        fixture.Inventory.Setup(repository => repository.GetAllProductsAsync(10, null)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetStockByBranchAsync(30, 10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetCategoriesAsync(10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetUnitsAsync(10)).ReturnsAsync([]);

        var response = await fixture.Service.ChatAsync(10, 20, 30, "Walos", action, null);

        Assert.Contains("no esta disponible en V1", response.Message);
        fixture.Inventory.Verify(repository => repository.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        fixture.Inventory.Verify(repository => repository.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Theory]
    [InlineData("add_stock")]
    [InlineData("create_and_stock")]
    public async Task Direct_Stock_Mutation_ToolName_Is_Rejected_Without_Any_Stock_Mutation(
        string toolName)
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Inventory);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("inventory");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync($"{{\"toolName\":\"{toolName}\",\"action\":\"query\",\"response\":\"stock agregado\"}}");
        fixture.Inventory.Setup(repository => repository.GetAllProductsAsync(10, null)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetStockByBranchAsync(30, 10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetCategoriesAsync(10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetUnitsAsync(10)).ReturnsAsync([]);

        var response = await fixture.Service.ChatAsync(10, 20, 30, "Walos", toolName, null);

        Assert.Contains("no esta disponible en V1", response.Message);
        fixture.Inventory.Verify(repository => repository.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        fixture.Inventory.Verify(repository => repository.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Theory]
    [InlineData("add_stock")]
    [InlineData("create_and_stock")]
    public async Task Purchase_Checklist_Action_With_Stock_Mutation_ToolName_Is_Rejected_Before_Flow_Start(
        string toolName)
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Inventory, WalosFeatures.Purchases);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("inventory");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync($"{{\"action\":\"show_low_stock_checklist\",\"toolName\":\"{toolName}\",\"response\":\"pedido\"}}");
        fixture.Inventory.Setup(repository => repository.GetAllProductsAsync(10, null)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetStockByBranchAsync(30, 10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetCategoriesAsync(10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetUnitsAsync(10)).ReturnsAsync([]);

        var response = await fixture.Service.ChatAsync(10, 20, 30, "Walos", "crear pedido", null);

        Assert.Contains("no esta disponible en V1", response.Message);
        fixture.Sessions.Verify(repository => repository.UpdateSessionContextAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
            It.Is<string>(json => json.Contains("\"flow\"")), It.IsAny<string>()), Times.Never);
        fixture.Inventory.Verify(repository => repository.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        fixture.Inventory.Verify(repository => repository.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Theory]
    [InlineData("add_stock")]
    [InlineData("create_and_stock")]
    public async Task Delivery_Dispatcher_Rejects_Stock_Mutation_ToolName_Without_Inventory_Mutation(
        string toolName)
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Delivery);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("delivery");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync($"{{\"action\":\"query\",\"tool_name\":\"{toolName}\",\"response\":\"stock agregado\"}}");
        fixture.Delivery.Setup(repository => repository.GetOrdersAsync(
            10, 30, null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync([]);

        var response = await fixture.Service.ChatAsync(10, 20, 30, "Walos", toolName, null);

        Assert.Contains("no esta disponible en V1", response.Message);
        fixture.Inventory.Verify(repository => repository.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        fixture.Inventory.Verify(repository => repository.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Theory]
    [InlineData("add_stock")]
    [InlineData("create_and_stock")]
    public async Task Purchase_Checklist_Flow_Rejects_Stock_Mutation_ToolName_Without_Inventory_Mutation(
        string toolName)
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Purchases);
        fixture.Session.Context = "{\"capability_fingerprint\":\"current\",\"branch_id\":30,\"flow\":\"awaiting_checklist_confirmation\",\"flow_capability\":\"purchases\",\"checklist_items\":\"[]\"}";
        fixture.Suppliers.Setup(repository => repository.GetAllAsync(10, null)).ReturnsAsync([]);
        fixture.Ai.Setup(service => service.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync($"{{\"action\":\"create_order\",\"toolName\":\"{toolName}\",\"response\":\"stock agregado\"}}");

        var response = await fixture.Service.ChatAsync(10, 20, 30, "Walos", toolName, null);

        Assert.Contains("no esta disponible en V1", response.Message);
        fixture.Inventory.Verify(repository => repository.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        fixture.Inventory.Verify(repository => repository.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Fact]
    public async Task Branch_Change_Resets_Session_And_Does_Not_Send_Previous_Branch_History()
    {
        var fixture = new Fixture(WalosFeatures.Ai);
        fixture.Session.Context = "{\"capability_fingerprint\":\"current\",\"branch_id\":30}";
        fixture.Sessions.Setup(repository => repository.GetMessagesAsync(fixture.Session.Id, 10, 20, It.IsAny<int>()))
            .ReturnsAsync([
                new AiMessage
                {
                    Role = "user",
                    Content = "branch 30 secret",
                    Metadata = "{\"capability_fingerprint\":\"current\",\"branch_id\":30}"
                }
            ]);
        fixture.Ai.Setup(service => service.ClassifyAsync(
                It.Is<string>(prompt => !prompt.Contains("branch 30 secret"))))
            .ReturnsAsync("general");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.Is<string>(prompt => !prompt.Contains("branch 30 secret")),
                It.IsAny<string>(),
                null))
            .ReturnsAsync("respuesta");

        await fixture.Service.ChatAsync(10, 20, 31, "Walos", "hola", null);

        fixture.Sessions.Verify(repository => repository.ResetSessionAsync(
            fixture.Session.Id, 10, 20,
            It.Is<string>(json => json.Contains("\"branch_id\":31") && !json.Contains("branch 30 secret"))), Times.Once);
        fixture.Ai.VerifyAll();
    }

    [Fact]
    public async Task Purchase_Checklist_When_Purchases_Disabled_Propagates_403_And_Does_Not_Persist_Denied_Exchange()
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Inventory);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("inventory");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.Is<string>(prompt => !prompt.Contains("add_stock", StringComparison.Ordinal)),
                It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync("{\"action\":\"show_low_stock_checklist\",\"response\":\"pedido\"}");
        fixture.Inventory.Setup(repository => repository.GetAllProductsAsync(10, null)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetStockByBranchAsync(30, 10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetCategoriesAsync(10)).ReturnsAsync([]);
        fixture.Inventory.Setup(repository => repository.GetUnitsAsync(10)).ReturnsAsync([]);

        var error = await Assert.ThrowsAsync<FeatureNotEnabledException>(() =>
            fixture.Service.ChatAsync(10, 20, 30, "Walos", "crear pedido", null));

        Assert.Equal(WalosFeatures.Purchases, error.Feature);
        fixture.Sessions.Verify(repository => repository.AddMessageAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Suppliers_Response_Uses_Suppliers_Agent_Type()
    {
        var fixture = new Fixture(WalosFeatures.Ai, WalosFeatures.Suppliers);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync("suppliers");
        fixture.Ai.Setup(service => service.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>()))
            .ReturnsAsync("Proveedor Uno");
        fixture.Suppliers.Setup(repository => repository.GetAllAsync(10, null)).ReturnsAsync([]);

        var response = await fixture.Service.ChatAsync(10, 20, 30, "Walos", "proveedores", null);

        Assert.Equal("suppliers", response.AgentType);
    }

    [Theory]
    [InlineData("purchases", WalosFeatures.Purchases)]
    [InlineData("suppliers", WalosFeatures.Suppliers)]
    [InlineData("delivery", WalosFeatures.Delivery)]
    public async Task Disabled_Specialized_Capability_Fails_Before_Its_Repositories(
        string classifiedIntent,
        string requiredFeature)
    {
        var fixture = new Fixture(WalosFeatures.Ai);
        fixture.Ai.Setup(service => service.ClassifyAsync(It.IsAny<string>())).ReturnsAsync(classifiedIntent);

        var error = await Assert.ThrowsAsync<FeatureNotEnabledException>(() =>
            fixture.Service.ChatAsync(10, 20, 30, "Walos", "consulta", null));

        Assert.Equal(requiredFeature, error.Feature);
        fixture.Inventory.Verify(repository => repository.GetAllProductsAsync(
            It.IsAny<long>(), It.IsAny<ProductFilter?>()), Times.Never);
        fixture.Suppliers.Verify(repository => repository.GetAllAsync(
            It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
        fixture.Delivery.Verify(repository => repository.GetOrdersAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Never);
    }

    private sealed class Fixture
    {
        public readonly Mock<IAiSessionRepository> Sessions = new();
        public readonly Mock<IAiService> Ai = new();
        public readonly Mock<IInventoryRepository> Inventory = new();
        public readonly Mock<IDeliveryRepository> Delivery = new();
        public readonly Mock<ISuppliersRepository> Suppliers = new();
        public readonly Mock<IAiCapabilityGuard> Capabilities = new();
        public readonly AiSession Session = new() { Id = 40, CompanyId = 10, UserId = 20 };
        public readonly OrchestratorService Service;

        public Fixture(params string[] enabled)
        {
            var states = enabled.ToDictionary(code => code, _ => true, StringComparer.Ordinal);
            var snapshot = new AiCapabilitySnapshot(states, "current");
            Session.Context = "{\"capability_fingerprint\":\"current\",\"branch_id\":30}";
            Capabilities.Setup(guard => guard.GetSnapshotAsync(10, false)).ReturnsAsync(snapshot);
            Sessions.Setup(repository => repository.AcquireConversationLockAsync(10, 20))
                .ReturnsAsync(new NoopAsyncDisposable());
            Sessions.Setup(repository => repository.GetOrCreateSessionAsync(10, 20)).ReturnsAsync(Session);
            Sessions.Setup(repository => repository.GetMessagesAsync(Session.Id, 10, 20, It.IsAny<int>())).ReturnsAsync([]);
            Service = new OrchestratorService(
                Sessions.Object, Ai.Object, Inventory.Object, Delivery.Object,
                Suppliers.Object, Capabilities.Object, NullLogger<OrchestratorService>.Instance);
        }

        private sealed class NoopAsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
