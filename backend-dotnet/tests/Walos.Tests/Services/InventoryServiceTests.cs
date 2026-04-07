using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class InventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _repoMock;
    private readonly Mock<IAiService> _aiMock;
    private readonly Mock<ILogger<InventoryService>> _loggerMock;
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _repoMock = new Mock<IInventoryRepository>();
        _aiMock = new Mock<IAiService>();
        _loggerMock = new Mock<ILogger<InventoryService>>();
        _service = new InventoryService(_repoMock.Object, _aiMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetLowStockProducts_ReturnsOnlyLowAndOutStock()
    {
        var stockList = new List<Stock>
        {
            new() { ProductId = 1, ProductName = "Ron", Quantity = 2, StockStatus = "low" },
            new() { ProductId = 2, ProductName = "Vodka", Quantity = 50, StockStatus = "ok" },
            new() { ProductId = 3, ProductName = "Whisky", Quantity = 0, StockStatus = "out" }
        };

        _repoMock.Setup(r => r.GetStockByBranchAsync(1, 1))
            .ReturnsAsync(stockList);

        var result = (await _service.GetLowStockProductsAsync(1, 1)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.ProductName == "Ron");
        Assert.Contains(result, s => s.ProductName == "Whisky");
        Assert.DoesNotContain(result, s => s.ProductName == "Vodka");
    }

    [Fact]
    public async Task ConfirmAiAction_ThrowsNotFound_WhenInteractionMissing()
    {
        _repoMock.Setup(r => r.GetAiInteractionByIdAsync(999, 1))
            .ReturnsAsync((AiInteraction?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ConfirmAiActionAsync(999, 1, 1));
    }

    [Fact]
    public async Task ProcessAiInventoryInput_CallsAiServiceAndSavesInteraction()
    {
        _repoMock.Setup(r => r.GetAllProductsAsync(1, null))
            .ReturnsAsync(new List<Product> { new() { Id = 1, Name = "Ron" } });

        _aiMock.Setup(a => a.ProcessInventoryInputAsync(It.IsAny<string>(), It.IsAny<AiContext>()))
            .ReturnsAsync(new AiInventoryResponse
            {
                Action = "add_stock",
                Confidence = 95,
                Response = "Voy a agregar 10 unidades de Ron",
                Data = new AiInventoryData
                {
                    Products = new List<AiProductEntry>
                    {
                        new() { Name = "Ron", Quantity = 10, UnitCost = 15 }
                    },
                    Total = 150
                },
                Metadata = new AiMetadata { Model = "gpt-4", TokensUsed = 250 }
            });

        _repoMock.Setup(r => r.SaveAiInteractionAsync(It.IsAny<AiInteraction>()))
            .ReturnsAsync(new AiInteraction { Id = 42, CompanyId = 1 });

        var context = new AiInputContext
        {
            CompanyId = 1,
            BranchId = 1,
            UserId = 1,
            InputType = "text"
        };

        var result = await _service.ProcessAiInventoryInputAsync("Llegaron 10 Ron a $15", context);

        Assert.Equal(42, result.InteractionId);
        Assert.Equal("add_stock", result.Action);
        Assert.Equal(95, result.Confidence);
        Assert.False(result.RequiresConfirmation);

        _repoMock.Verify(r => r.SaveAiInteractionAsync(It.IsAny<AiInteraction>()), Times.Once);
    }
}
