using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class SalesServiceTests
{
    private readonly Mock<ISalesRepository> _salesRepoMock;
    private readonly Mock<IInventoryRepository> _inventoryRepoMock;
    private readonly Mock<ICompanyRepository> _companyRepoMock;
    private readonly Mock<ILogger<SalesService>> _loggerMock;
    private readonly SalesService _service;

    private const long CompanyId = 1;
    private const long BranchId = 10;
    private const long UserId = 100;

    public SalesServiceTests()
    {
        _salesRepoMock = new Mock<ISalesRepository>();
        _inventoryRepoMock = new Mock<IInventoryRepository>();
        _companyRepoMock = new Mock<ICompanyRepository>();
        _loggerMock = new Mock<ILogger<SalesService>>();
        _service = new SalesService(
            _salesRepoMock.Object,
            _inventoryRepoMock.Object,
            _companyRepoMock.Object,
            _loggerMock.Object);
    }

    // ── CreateTableAsync ──

    [Fact]
    public async Task CreateTable_ThrowsValidation_WhenNoItems()
    {
        var request = new CreateTableRequest { Items = new List<CreateTableItemDto>() };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, request));
    }

    [Fact]
    public async Task CreateTable_ThrowsValidation_WhenProductNotFound()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(99, CompanyId))
            .ReturnsAsync((Product?)null);

        var request = new CreateTableRequest
        {
            Items = new List<CreateTableItemDto>
            {
                new() { ProductId = 99, ProductName = "X", Quantity = 1, UnitPrice = 10 }
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, request));
    }

    [Fact]
    public async Task CreateTable_ThrowsValidation_WhenProductInactive()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product { Id = 1, Name = "Ron", IsActive = false, TrackStock = false });

        var request = new CreateTableRequest
        {
            Items = new List<CreateTableItemDto>
            {
                new() { ProductId = 1, ProductName = "Ron", Quantity = 1, UnitPrice = 10 }
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, request));
    }

    [Fact]
    public async Task CreateTable_ThrowsValidation_WhenInsufficientStock()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product { Id = 1, Name = "Ron", IsActive = true, TrackStock = true });
        _inventoryRepoMock.Setup(r => r.GetStockByProductAsync(BranchId, 1, CompanyId))
            .ReturnsAsync(new Stock { ProductId = 1, ProductName = "Ron", AvailableQuantity = 2, ReservedQuantity = 0 });

        var request = new CreateTableRequest
        {
            Items = new List<CreateTableItemDto>
            {
                new() { ProductId = 1, ProductName = "Ron", Quantity = 5, UnitPrice = 10 }
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, request));
    }

    [Fact]
    public async Task CreateTable_SkipsStockCheck_WhenProductDoesNotTrackStock()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product { Id = 1, Name = "Servicio", IsActive = true, TrackStock = false });
        _salesRepoMock.Setup(r => r.GetNextTableNumberAsync(CompanyId, BranchId)).ReturnsAsync(1);
        _salesRepoMock.Setup(r => r.CreateTableAsync(It.IsAny<SalesTable>()))
            .ReturnsAsync((SalesTable t) => { t.Id = 50; return t; });
        _salesRepoMock.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()))
            .ReturnsAsync(new Order { Id = 1 });

        var request = new CreateTableRequest
        {
            Items = new List<CreateTableItemDto>
            {
                new() { ProductId = 1, ProductName = "Servicio", Quantity = 100, UnitPrice = 5 }
            }
        };

        var result = await _service.CreateTableAsync(CompanyId, BranchId, UserId, request);

        Assert.Equal(500, result.Total);
        Assert.Equal(1, result.ItemCount);
        _inventoryRepoMock.Verify(r => r.GetStockByProductAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task CreateTable_Success_ReturnsResult()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product { Id = 1, Name = "Ron", IsActive = true, TrackStock = true });
        _inventoryRepoMock.Setup(r => r.GetStockByProductAsync(BranchId, 1, CompanyId))
            .ReturnsAsync(new Stock { ProductId = 1, ProductName = "Ron", AvailableQuantity = 20, ReservedQuantity = 0 });
        _salesRepoMock.Setup(r => r.GetNextTableNumberAsync(CompanyId, BranchId)).ReturnsAsync(3);
        _salesRepoMock.Setup(r => r.CreateTableAsync(It.IsAny<SalesTable>()))
            .ReturnsAsync((SalesTable t) => { t.Id = 50; return t; });
        _salesRepoMock.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()))
            .ReturnsAsync(new Order { Id = 1 });

        var request = new CreateTableRequest
        {
            Items = new List<CreateTableItemDto>
            {
                new() { ProductId = 1, ProductName = "Ron", Quantity = 2, UnitPrice = 25 }
            }
        };

        var result = await _service.CreateTableAsync(CompanyId, BranchId, UserId, request);

        Assert.Equal(50, result.Total);
        Assert.Equal(1, result.ItemCount);
        Assert.Equal("Mesa 3", result.Table.Name);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Once);
    }

    // ── InvoiceTableAsync ──

    [Fact]
    public async Task InvoiceTable_ThrowsNotFound_WhenTableMissing()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(99, CompanyId)).ReturnsAsync((SalesTable?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 99, new InvoiceTableRequest()));
    }

    [Fact]
    public async Task InvoiceTable_ThrowsBusiness_WhenTableNotOpen()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(1, CompanyId))
            .ReturnsAsync(new SalesTable { Id = 1, Status = "invoiced" });

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 1, new InvoiceTableRequest()));
    }

    [Fact]
    public async Task InvoiceTable_ThrowsValidation_WhenInvalidDiscountType()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(1, CompanyId))
            .ReturnsAsync(new SalesTable { Id = 1, Status = "open", TableNumber = 1 });
        _salesRepoMock.Setup(r => r.GetOrderByTableIdAsync(1, CompanyId))
            .ReturnsAsync(new Order { Id = 10, Subtotal = 100 });
        _salesRepoMock.Setup(r => r.GetOrderItemsAsync(10, CompanyId))
            .ReturnsAsync(new List<OrderItem>());

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 1,
                new InvoiceTableRequest { DiscountType = "invalid" }));
    }

    [Fact]
    public async Task InvoiceTable_ThrowsBusiness_WhenDiscountDisabled()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(1, CompanyId))
            .ReturnsAsync(new SalesTable { Id = 1, Status = "open", TableNumber = 1 });
        _salesRepoMock.Setup(r => r.GetOrderByTableIdAsync(1, CompanyId))
            .ReturnsAsync(new Order { Id = 10, Subtotal = 100 });
        _salesRepoMock.Setup(r => r.GetOrderItemsAsync(10, CompanyId))
            .ReturnsAsync(new List<OrderItem>());
        _companyRepoMock.Setup(r => r.GetCompanyOperationsSettingsAsync(CompanyId))
            .ReturnsAsync(new CompanyOperationsSettings { ManualDiscountEnabled = false });

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 1,
                new InvoiceTableRequest { DiscountType = "percentage", DiscountValue = 10 }));
    }

    [Fact]
    public async Task InvoiceTable_Success_NoDiscount()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(1, CompanyId))
            .ReturnsAsync(new SalesTable { Id = 1, Status = "open", TableNumber = 5 });
        _salesRepoMock.Setup(r => r.GetOrderByTableIdAsync(1, CompanyId))
            .ReturnsAsync(new Order { Id = 10, Subtotal = 200, OrderNumber = "ORD-10" });
        _salesRepoMock.Setup(r => r.GetOrderItemsAsync(10, CompanyId))
            .ReturnsAsync(new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 2, UnitPrice = 100 }
            });

        var result = await _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 1, new InvoiceTableRequest());

        Assert.Equal(200, result.FinalTotalPaid);
        Assert.Equal(0, result.DiscountAmount);
        Assert.Equal("none", result.DiscountType);
        _salesRepoMock.Verify(r => r.UpdateOrderStatusAsync(10, CompanyId, "completed"), Times.Once);
        _salesRepoMock.Verify(r => r.UpdateTableStatusAsync(1, CompanyId, "invoiced"), Times.Once);
    }

    // ── CancelTableAsync ──

    [Fact]
    public async Task CancelTable_ThrowsNotFound_WhenTableMissing()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(99, CompanyId)).ReturnsAsync((SalesTable?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CancelTableAsync(CompanyId, 99));
    }

    [Fact]
    public async Task CancelTable_CancelsOrderAndTable()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(1, CompanyId))
            .ReturnsAsync(new SalesTable { Id = 1, TableNumber = 1 });
        _salesRepoMock.Setup(r => r.GetOrderByTableIdAsync(1, CompanyId))
            .ReturnsAsync(new Order { Id = 10 });

        await _service.CancelTableAsync(CompanyId, 1);

        _salesRepoMock.Verify(r => r.UpdateOrderStatusAsync(10, CompanyId, "cancelled"), Times.Once);
        _salesRepoMock.Verify(r => r.UpdateTableStatusAsync(1, CompanyId, "cancelled"), Times.Once);
    }

    // ── UpdateItemQuantityAsync ──

    [Fact]
    public async Task UpdateItemQuantity_ThrowsValidation_WhenNegative()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateItemQuantityAsync(CompanyId, BranchId, 1, new UpdateItemQuantityRequest { Quantity = -1 }));
    }

    [Fact]
    public async Task UpdateItemQuantity_ThrowsNotFound_WhenItemMissing()
    {
        _salesRepoMock.Setup(r => r.GetOrderItemByIdAsync(99, CompanyId)).ReturnsAsync((OrderItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateItemQuantityAsync(CompanyId, BranchId, 99, new UpdateItemQuantityRequest { Quantity = 1 }));
    }

    [Fact]
    public async Task UpdateItemQuantity_DeletesItem_WhenQuantityZero()
    {
        _salesRepoMock.Setup(r => r.GetOrderItemByIdAsync(1, CompanyId))
            .ReturnsAsync(new OrderItem { Id = 1, OrderId = 10, ProductId = 1, Quantity = 3 });
        _salesRepoMock.Setup(r => r.GetOrderByIdAsync(10, CompanyId))
            .ReturnsAsync(new Order { Id = 10, BranchId = BranchId });

        await _service.UpdateItemQuantityAsync(CompanyId, BranchId, 1,
            new UpdateItemQuantityRequest { Quantity = 0, OrderId = 10 });

        _salesRepoMock.Verify(r => r.DeleteOrderItemAsync(1, CompanyId), Times.Once);
        _salesRepoMock.Verify(r => r.RecalculateOrderTotalAsync(10, CompanyId), Times.Once);
    }

    // ── AddItemsToTableAsync ──

    [Fact]
    public async Task AddItems_ThrowsNotFound_WhenTableMissing()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(99, CompanyId)).ReturnsAsync((SalesTable?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddItemsToTableAsync(CompanyId, 99, new List<CreateTableItemDto>()));
    }

    [Fact]
    public async Task AddItems_ThrowsBusiness_WhenTableNotOpen()
    {
        _salesRepoMock.Setup(r => r.GetTableByIdAsync(1, CompanyId))
            .ReturnsAsync(new SalesTable { Id = 1, Status = "invoiced" });

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.AddItemsToTableAsync(CompanyId, 1, new List<CreateTableItemDto>()));
    }
}
