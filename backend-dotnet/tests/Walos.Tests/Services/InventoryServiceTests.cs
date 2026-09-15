using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Application.Storage;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class InventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _repoMock;
    private readonly Mock<IAiService> _aiMock;
    private readonly Mock<IFileStorage> _fileStorageMock;
    private readonly Mock<IAiCapabilityGuard> _capabilityGuardMock;
    private readonly Mock<ILogger<InventoryService>> _loggerMock;
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _repoMock = new Mock<IInventoryRepository>();
        _aiMock = new Mock<IAiService>();
        _fileStorageMock = new Mock<IFileStorage>();
        _capabilityGuardMock = new Mock<IAiCapabilityGuard>();
        _capabilityGuardMock
            .Setup(guard => guard.GetSnapshotAsync(It.IsAny<long>(), It.IsAny<bool>()))
            .ReturnsAsync(new AiCapabilitySnapshot(new Dictionary<string, bool>
            {
                [Walos.Domain.Features.WalosFeatures.Ai] = true,
                [Walos.Domain.Features.WalosFeatures.Inventory] = true
            }, "test"));
        _loggerMock = new Mock<ILogger<InventoryService>>();
        _service = new InventoryService(
            _repoMock.Object,
            _aiMock.Object,
            _fileStorageMock.Object,
            _capabilityGuardMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UploadProductImage_UsesTenantCas_AndDeletesPreviousManagedObject()
    {
        const string previous = "companies/7/products/11/old.webp";
        const string current = "companies/7/products/11/new.webp";
        _repoMock.Setup(repository => repository.GetProductByIdAsync(11, 7))
            .ReturnsAsync(new Product { Id = 11, CompanyId = 7, ImageUrl = previous });
        _fileStorageMock.Setup(storage => storage.UploadImageAsync(
                It.Is<ImageUploadRequest>(request =>
                    request.CompanyId == 7 &&
                    request.Scope == ImageStorageScope.Product &&
                    request.ProductId == 11),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFile(current, "https://storage.test/new.webp", "image/webp", 10, 1, 1));
        _repoMock.Setup(repository => repository.TryUpdateProductImageAsync(11, 7, previous, current))
            .ReturnsAsync(true);
        _fileStorageMock.Setup(storage => storage.IsManagedReference(previous)).Returns(true);
        _fileStorageMock.Setup(storage => storage.DeleteIfManagedAsync(previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UploadProductImageAsync(
            11, 7, new MemoryStream([1]), "new.webp", "image/webp");

        Assert.Equal(current, result.ObjectKey);
        _repoMock.Verify(
            repository => repository.TryUpdateProductImageAsync(11, 7, previous, current),
            Times.Once);
        _fileStorageMock.Verify(
            storage => storage.DeleteIfManagedAsync(previous, It.IsAny<CancellationToken>()),
            Times.Once);
        _fileStorageMock.Verify(
            storage => storage.DeleteIfManagedAsync(current, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadProductImage_CompensatesNewObject_WhenCasLosesRace()
    {
        const string current = "companies/7/products/11/new.webp";
        _repoMock.Setup(repository => repository.GetProductByIdAsync(11, 7))
            .ReturnsAsync(new Product { Id = 11, CompanyId = 7, ImageUrl = null });
        _fileStorageMock.Setup(storage => storage.UploadImageAsync(
                It.IsAny<ImageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFile(current, "https://storage.test/new.webp", "image/webp", 10, 1, 1));
        _repoMock.Setup(repository => repository.TryUpdateProductImageAsync(11, 7, null, current))
            .ReturnsAsync(false);
        _fileStorageMock.Setup(storage => storage.DeleteIfManagedAsync(current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.UploadProductImageAsync(
                11, 7, new MemoryStream([1]), "new.webp", "image/webp"));

        Assert.Equal("PRODUCT_IMAGE_CONFLICT", exception.Code);
        _fileStorageMock.Verify(
            storage => storage.DeleteIfManagedAsync(current, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadProductImage_CompensatesNewObject_WhenDatabaseUpdateFails()
    {
        const string current = "companies/7/products/11/new.webp";
        _repoMock.Setup(repository => repository.GetProductByIdAsync(11, 7))
            .ReturnsAsync(new Product { Id = 11, CompanyId = 7 });
        _fileStorageMock.Setup(storage => storage.UploadImageAsync(
                It.IsAny<ImageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFile(current, "https://storage.test/new.webp", "image/webp", 10, 1, 1));
        _repoMock.Setup(repository => repository.TryUpdateProductImageAsync(11, 7, null, current))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        _fileStorageMock.Setup(storage => storage.DeleteIfManagedAsync(current, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadProductImageAsync(
                11, 7, new MemoryStream([1]), "new.webp", "image/webp"));

        _fileStorageMock.Verify(
            storage => storage.DeleteIfManagedAsync(current, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLowStockProducts_ReturnsOnlyLowAndReorderStock()
    {
        var stockList = new List<Stock>
        {
            new() { ProductId = 1, ProductName = "Ron", Quantity = 2, StockStatus = "low" },
            new() { ProductId = 2, ProductName = "Vodka", Quantity = 50, StockStatus = "ok" },
            new() { ProductId = 3, ProductName = "Whisky", Quantity = 8, StockStatus = "reorder" }
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
    public async Task ConfirmAiAction_Is_Disabled_Before_Reading_Pending_Interaction()
    {
        var result = await _service.ConfirmAiActionAsync(999, 1, 1);

        Assert.False(result.Success);
        Assert.Contains("no esta disponible en V1", result.Message);
        _repoMock.Verify(r => r.GetAiInteractionByIdAsync(It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAiInventoryInput_Does_Not_Persist_Mutation_Proposal()
    {
        _repoMock.Setup(r => r.GetAllProductsAsync(1, It.Is<ProductFilter?>(f => f == null)))
            .ReturnsAsync(new List<Product> { new() { Id = 1, Name = "Ron" } });

        _aiMock.Setup(a => a.ProcessInventoryInputAsync(
                It.IsAny<string>(),
                It.IsAny<AiContext>(),
                It.IsAny<List<AiConversationMessage>?>()))
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

        Assert.Equal(0, result.InteractionId);
        Assert.Equal("stock_mutation_disabled", result.Action);
        Assert.Equal(95, result.Confidence);
        Assert.False(result.RequiresConfirmation);

        _repoMock.Verify(r => r.SaveAiInteractionAsync(It.IsAny<AiInteraction>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAiInventoryInput_Keeps_Read_Only_Query_And_Uses_No_Prior_History()
    {
        _repoMock.Setup(r => r.GetAllProductsAsync(1, It.Is<ProductFilter?>(f => f == null)))
            .ReturnsAsync([]);
        _aiMock.Setup(service => service.ProcessInventoryInputAsync(
                "consulta stock",
                It.IsAny<AiContext>(),
                null))
            .ReturnsAsync(new AiInventoryResponse
            {
                Action = "query",
                Response = "Sin faltantes",
                Confidence = 100
            });
        _repoMock.Setup(repository => repository.SaveAiInteractionAsync(It.IsAny<AiInteraction>()))
            .ReturnsAsync(new AiInteraction { Id = 55, CompanyId = 1 });

        var result = await _service.ProcessAiInventoryInputAsync(
            "consulta stock",
            new AiInputContext { CompanyId = 1, UserId = 2, BranchId = 3, SessionId = "legacy" });

        Assert.Equal("query", result.Action);
        Assert.Equal(55, result.InteractionId);
        _repoMock.Verify(repository => repository.GetAiInteractionsBySessionAsync(
            It.IsAny<string>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAiInventoryInput_Fails_Closed_Before_Repository_When_Inventory_Is_Disabled()
    {
        _capabilityGuardMock
            .Setup(guard => guard.GetSnapshotAsync(1, false))
            .ReturnsAsync(new AiCapabilitySnapshot(new Dictionary<string, bool>
            {
                [Walos.Domain.Features.WalosFeatures.Ai] = true
            }, "disabled"));

        var error = await Assert.ThrowsAsync<FeatureNotEnabledException>(() =>
            _service.ProcessAiInventoryInputAsync("consulta", new AiInputContext { CompanyId = 1 }));

        Assert.Equal(Walos.Domain.Features.WalosFeatures.Inventory, error.Feature);
        _repoMock.Verify(r => r.GetAllProductsAsync(
            It.IsAny<long>(), It.IsAny<ProductFilter?>()), Times.Never);
        _aiMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddStock_Rejects_Body_Branch_That_Overrides_Jwt_Before_Any_Write()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.AddStockAsync(
            1, 10, 20, new AddStockRequest { ProductId = 30, BranchId = 21, Quantity = 2 }));

        _repoMock.Verify(r => r.UpdateProductCostAndPriceAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<decimal?>()), Times.Never);
        _repoMock.Verify(r => r.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        _repoMock.Verify(r => r.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Fact]
    public async Task AddStock_Rejects_Foreign_Branch_For_CompanyWide_User_Before_Any_Write()
    {
        _repoMock.Setup(r => r.IsActiveBranchInCompanyAsync(99, 1)).ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AddStockAsync(
            1, 10, null, new AddStockRequest { ProductId = 30, BranchId = 99, Quantity = 2 }));

        _repoMock.Verify(r => r.UpdateStockAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
        _repoMock.Verify(r => r.CreateMovementAsync(It.IsAny<Movement>()), Times.Never);
    }

    [Fact]
    public async Task AddStock_Uses_Authenticated_Branch_When_Body_Omits_It()
    {
        _repoMock.Setup(r => r.IsActiveBranchInCompanyAsync(20, 1)).ReturnsAsync(true);
        _repoMock.Setup(r => r.GetProductByIdAsync(30, 1))
            .ReturnsAsync(new Product { Id = 30, CompanyId = 1, IsActive = true });
        _repoMock.Setup(r => r.UpdateStockAsync(20, 30, 2, 1))
            .ReturnsAsync(new Stock { CompanyId = 1, BranchId = 20, ProductId = 30, Quantity = 2 });
        _repoMock.Setup(r => r.CreateMovementAsync(It.IsAny<Movement>()))
            .ReturnsAsync((Movement m) => m);

        var result = await _service.AddStockAsync(
            1, 10, 20, new AddStockRequest { ProductId = 30, Quantity = 2 });

        Assert.Equal(20, result.BranchId);
        _repoMock.Verify(r => r.UpdateStockAsync(20, 30, 2, 1), Times.Once);
        _repoMock.Verify(r => r.CreateMovementAsync(It.Is<Movement>(m =>
            m.CompanyId == 1 && m.BranchId == 20 && m.ProductId == 30)), Times.Once);
    }

    [Fact]
    public async Task CreateProduct_Rejects_Category_From_Another_Company()
    {
        _repoMock.Setup(r => r.IsActiveCategoryInCompanyAsync(50, 1)).ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateProductAsync(
            1, 10, 20, ValidProductRequest(categoryId: 50, unitId: 60)));

        _repoMock.Verify(r => r.CreateProductAsync(It.IsAny<Product>()), Times.Never);
        _repoMock.Verify(r => r.CreateStockEntryAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_Rejects_Unit_From_Another_Company()
    {
        _repoMock.Setup(r => r.IsActiveCategoryInCompanyAsync(50, 1)).ReturnsAsync(true);
        _repoMock.Setup(r => r.IsActiveUnitInCompanyAsync(60, 1)).ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateProductAsync(
            1, 10, 20, ValidProductRequest(categoryId: 50, unitId: 60)));

        _repoMock.Verify(r => r.CreateProductAsync(It.IsAny<Product>()), Times.Never);
    }

    private static CreateProductRequest ValidProductRequest(long categoryId, long unitId) => new()
    {
        Name = "Producto",
        Sku = "SKU-TEST",
        CategoryId = categoryId,
        UnitId = unitId,
        ProductType = "simple",
        TrackStock = true,
        IsForSale = true,
    };
}
