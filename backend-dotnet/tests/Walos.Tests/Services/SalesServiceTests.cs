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
    private readonly Mock<IRecipeRepository> _recipeRepoMock;
    private readonly Mock<ICreditRepository> _creditRepoMock;
    private readonly Mock<ICashRegisterRepository> _cashRegisterRepoMock;
    private readonly Mock<IOrderPaymentRepository> _orderPaymentRepoMock;
    private readonly Mock<IUsersRepository> _usersRepoMock;
    private readonly Mock<IRefundRepository> _refundRepoMock;
    private readonly Mock<ICheckoutRepository> _checkoutRepoMock;
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
        _recipeRepoMock = new Mock<IRecipeRepository>();
        _creditRepoMock = new Mock<ICreditRepository>();
        _cashRegisterRepoMock = new Mock<ICashRegisterRepository>();
        _orderPaymentRepoMock = new Mock<IOrderPaymentRepository>();
        _usersRepoMock = new Mock<IUsersRepository>();
        _refundRepoMock = new Mock<IRefundRepository>();
        _checkoutRepoMock = new Mock<ICheckoutRepository>();
        _loggerMock = new Mock<ILogger<SalesService>>();
        _service = new SalesService(
            _salesRepoMock.Object,
            _inventoryRepoMock.Object,
            _companyRepoMock.Object,
            _recipeRepoMock.Object,
            _creditRepoMock.Object,
            _cashRegisterRepoMock.Object,
            _orderPaymentRepoMock.Object,
            _usersRepoMock.Object,
            _refundRepoMock.Object,
            _checkoutRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ResolveBranch_Rejects_RequestOverride_ForBranchBoundUser()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ResolveBranchAsync(CompanyId, BranchId, BranchId + 1, required: true));

        _inventoryRepoMock.Verify(repository => repository.IsActiveBranchInCompanyAsync(
            It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ResolveBranch_ReturnsNotFound_ForForeignOrInactiveCompanyWideSelection()
    {
        _inventoryRepoMock.Setup(repository => repository.IsActiveBranchInCompanyAsync(99, CompanyId))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ResolveBranchAsync(CompanyId, null, 99, required: true));
    }

    [Fact]
    public async Task ResolveBranch_AllowsActiveSameCompanyBranch_ForCompanyWideUser()
    {
        _inventoryRepoMock.Setup(repository => repository.IsActiveBranchInCompanyAsync(BranchId, CompanyId))
            .ReturnsAsync(true);

        var result = await _service.ResolveBranchAsync(CompanyId, null, BranchId, required: true);

        Assert.Equal(BranchId, result);
    }

    // ── CreateTableAsync ──

    [Fact]
    public async Task CreateTable_ThrowsValidation_WhenNoItems()
    {
        var request = new CreateTableRequest { Items = new List<CreateTableItemDto>() };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, request));

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
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

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
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

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
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

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
    }

    [Fact]
    public async Task CreateTable_SkipsStockCheck_WhenProductDoesNotTrackStock()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product { Id = 1, Name = "Servicio", SalePrice = 5, IsActive = true, TrackStock = false });
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
            .ReturnsAsync(new Product { Id = 1, Name = "Ron", SalePrice = 25, IsActive = true, TrackStock = true });
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

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(999)]
    public async Task CreateTable_UsesDatabasePrice_WhenClientPriceIsManipulated(int submittedPrice)
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Producto real",
                SalePrice = 125.50m,
                IsActive = true,
                IsForSale = true,
                TrackStock = false
            });
        _salesRepoMock.Setup(r => r.GetNextTableNumberAsync(CompanyId, BranchId)).ReturnsAsync(1);
        _salesRepoMock.Setup(r => r.CreateTableAsync(It.IsAny<SalesTable>()))
            .ReturnsAsync((SalesTable table) => { table.Id = 50; return table; });

        Order? persistedOrder = null;
        List<OrderItem>? persistedItems = null;
        _salesRepoMock.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()))
            .Callback<Order, List<OrderItem>>((order, items) =>
            {
                persistedOrder = order;
                persistedItems = items;
            })
            .ReturnsAsync(new Order { Id = 10 });

        var result = await _service.CreateTableAsync(CompanyId, BranchId, UserId, new CreateTableRequest
        {
            Items = [new CreateTableItemDto
            {
                ProductId = 1,
                ProductName = "Nombre manipulado",
                Quantity = 2,
                UnitPrice = submittedPrice
            }]
        });

        Assert.Equal(251m, result.Total);
        Assert.Equal(251m, persistedOrder!.Subtotal);
        Assert.Equal(125.50m, Assert.Single(persistedItems!).UnitPrice);
    }

    [Fact]
    public async Task CreateTable_UsesCanonicalProductName()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Nombre canonico",
                SalePrice = 20,
                IsActive = true,
                IsForSale = true,
                TrackStock = false
            });
        _salesRepoMock.Setup(r => r.GetNextTableNumberAsync(CompanyId, BranchId)).ReturnsAsync(1);
        _salesRepoMock.Setup(r => r.CreateTableAsync(It.IsAny<SalesTable>()))
            .ReturnsAsync((SalesTable table) => { table.Id = 50; return table; });

        List<OrderItem>? persistedItems = null;
        _salesRepoMock.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()))
            .Callback<Order, List<OrderItem>>((_, items) => persistedItems = items)
            .ReturnsAsync(new Order { Id = 10 });

        await _service.CreateTableAsync(CompanyId, BranchId, UserId, new CreateTableRequest
        {
            Items = [new CreateTableItemDto
            {
                ProductId = 1,
                ProductName = "Nombre falso",
                Quantity = 1,
                UnitPrice = 1
            }]
        });

        Assert.Equal("Nombre canonico", Assert.Single(persistedItems!).ProductName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateTable_RejectsNonPositiveQuantity_WithoutWrites(int quantity)
    {
        var request = new CreateTableRequest
        {
            Items = [new CreateTableItemDto { ProductId = 1, Quantity = quantity }]
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, request));

        _inventoryRepoMock.Verify(
            r => r.GetProductByIdAsync(It.IsAny<long>(), It.IsAny<long>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
    }

    [Fact]
    public async Task CreateTable_AllowsPositiveDecimalQuantity_ForWeightedProduct()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Producto pesado",
                ProductType = "weighted",
                SalePrice = 40,
                IsActive = true,
                IsForSale = true,
                TrackStock = false
            });
        _salesRepoMock.Setup(r => r.GetNextTableNumberAsync(CompanyId, BranchId)).ReturnsAsync(1);
        _salesRepoMock.Setup(r => r.CreateTableAsync(It.IsAny<SalesTable>()))
            .ReturnsAsync((SalesTable table) => { table.Id = 50; return table; });
        _salesRepoMock.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()))
            .ReturnsAsync(new Order { Id = 10 });

        var result = await _service.CreateTableAsync(CompanyId, BranchId, UserId, new CreateTableRequest
        {
            Items = [new CreateTableItemDto { ProductId = 1, Quantity = 1.25m }]
        });

        Assert.Equal(50m, result.Total);
        Assert.Equal(1.25m, Assert.Single(result.Table.Items!).Quantity);
    }

    [Fact]
    public async Task CreateTable_RejectsProductNotForSale_WithoutWrites()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Solo inventario",
                SalePrice = 20,
                IsActive = true,
                IsForSale = false,
                TrackStock = false
            });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = 1, Quantity = 1 }]
            }));

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
    }

    [Fact]
    public async Task CreateTable_RejectsNegativeDatabasePrice_WithoutWrites()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Precio invalido",
                SalePrice = -10,
                IsActive = true,
                IsForSale = true,
                TrackStock = false
            });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = 1, Quantity = 1, UnitPrice = 100 }]
            }));

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
    }

    [Fact]
    public async Task CreateTable_RejectsProductFromAnotherCompany_WithoutWrites()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(1, CompanyId))
            .ReturnsAsync((Product?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateTableAsync(CompanyId, BranchId, UserId, new CreateTableRequest
            {
                Items = [new CreateTableItemDto { ProductId = 1, Quantity = 1 }]
            }));

        _salesRepoMock.Verify(r => r.CreateTableAsync(It.IsAny<SalesTable>()), Times.Never);
        _salesRepoMock.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<List<OrderItem>>()), Times.Never);
    }

    // ── InvoiceTableAsync ──

    [Fact]
    public async Task InvoiceTable_Delegates_AtomicCheckout_AndMapsResult()
    {
        _checkoutRepoMock.Setup(repository => repository.ProcessAsync(It.IsAny<CheckoutCommand>()))
            .ReturnsAsync(new CheckoutResult
            {
                TableNumber = 5,
                OrderNumber = "ORD-10",
                Subtotal = 100m,
                DiscountType = "none",
                AmountPaid = 100m,
                SplitCount = 1,
                InvoicedAt = DateTime.UtcNow,
                Payments = [new CheckoutPayment("cash", 100m, null)]
            });

        var result = await _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 1,
            new InvoiceTableRequest
            {
                Payments = [new PaymentLineDto(" CASH ", 100m, null)]
            });

        Assert.Equal("ORD-10", result.OrderNumber);
        Assert.Equal(100m, result.FinalTotalPaid);
        _checkoutRepoMock.Verify(repository => repository.ProcessAsync(It.Is<CheckoutCommand>(command =>
            command.CompanyId == CompanyId &&
            command.BranchId == BranchId &&
            command.UserId == UserId &&
            command.TableId == 1 &&
            command.Payments.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task InvoiceTable_Propagates_ControlledCheckoutFailure()
    {
        _checkoutRepoMock.Setup(repository => repository.ProcessAsync(It.IsAny<CheckoutCommand>()))
            .ThrowsAsync(new BusinessException("La venta ya fue procesada", "checkout_already_processed"));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.InvoiceTableAsync(CompanyId, BranchId, UserId, 1, new InvoiceTableRequest()));

        Assert.Equal("checkout_already_processed", exception.Code);
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

    [Fact]
    public async Task CancelTable_ScopedBranch_DoesNotMutateTableFromAnotherBranch()
    {
        _salesRepoMock.Setup(repository => repository.GetTableByIdAsync(1, CompanyId, BranchId))
            .ReturnsAsync((SalesTable?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CancelTableAsync(CompanyId, BranchId, 1));

        _salesRepoMock.Verify(repository => repository.UpdateOrderStatusAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>()), Times.Never);
        _salesRepoMock.Verify(repository => repository.UpdateTableStatusAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOrderItems_ScopedBranch_DoesNotExposeOrderFromAnotherBranch()
    {
        _salesRepoMock.Setup(repository => repository.GetOrderByIdAsync(10, CompanyId, BranchId))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetOrderItemsAsync(CompanyId, BranchId, 10));

        _salesRepoMock.Verify(repository => repository.GetOrderItemsAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
    }

    // ── UpdateItemQuantityAsync ──

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateItemQuantity_RejectsNonPositiveQuantity_WithoutWrites(int quantity)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateItemQuantityAsync(
                CompanyId, BranchId, 1, new UpdateItemQuantityRequest { Quantity = quantity }));

        _salesRepoMock.Verify(repository => repository.GetOrderItemByIdAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
        _checkoutRepoMock.Verify(repository => repository.UpdateItemQuantityAsync(
            It.IsAny<UpdateOrderItemQuantityCommand>()), Times.Never);
    }

    [Fact]
    public async Task UpdateItemQuantity_ThrowsNotFound_WhenItemMissing()
    {
        _salesRepoMock.Setup(r => r.GetOrderItemByIdAsync(99, CompanyId, BranchId)).ReturnsAsync((OrderItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateItemQuantityAsync(CompanyId, BranchId, 99, new UpdateItemQuantityRequest { Quantity = 1 }));
    }

    [Fact]
    public async Task UpdateItemQuantity_RecalculatesActualOrder_IgnoringClientOrderId()
    {
        _salesRepoMock.Setup(r => r.GetOrderItemByIdAsync(1, CompanyId, BranchId))
            .ReturnsAsync(new OrderItem { Id = 1, OrderId = 10, ProductId = 1, Quantity = 3 });
        _salesRepoMock.Setup(r => r.GetOrderByIdAsync(10, CompanyId, BranchId))
            .ReturnsAsync(new Order { Id = 10, BranchId = BranchId });

        await _service.UpdateItemQuantityAsync(CompanyId, BranchId, 1,
            new UpdateItemQuantityRequest { Quantity = 2, OrderId = 999 });

        _checkoutRepoMock.Verify(r => r.UpdateItemQuantityAsync(It.Is<UpdateOrderItemQuantityCommand>(command =>
            command.OrderItemId == 1 && command.Quantity == 2)), Times.Once);
    }

    // ── AddItemsToTableAsync ──

    [Fact]
    public async Task AddItems_ThrowsNotFound_WhenTableMissing()
    {
        SetupSaleableProduct(5);
        _checkoutRepoMock.Setup(repository => repository.AddItemsAsync(It.IsAny<AddOrderItemsCommand>()))
            .ThrowsAsync(new NotFoundException("Mesa"));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddItemsToTableAsync(CompanyId, BranchId, 99,
                [new CreateTableItemDto { ProductId = 5, Quantity = 1 }]));
    }

    [Fact]
    public async Task AddItems_ThrowsBusiness_WhenTableNotOpen()
    {
        SetupSaleableProduct(5);
        _checkoutRepoMock.Setup(repository => repository.AddItemsAsync(It.IsAny<AddOrderItemsCommand>()))
            .ThrowsAsync(new BusinessException("La mesa ya fue procesada"));

        await Assert.ThrowsAsync<BusinessException>(() =>
            _service.AddItemsToTableAsync(CompanyId, BranchId, 1,
                [new CreateTableItemDto { ProductId = 5, Quantity = 1 }]));
    }

    [Fact]
    public async Task AddItems_UsesCanonicalProductData_WhenClientValuesAreManipulated()
    {
        _inventoryRepoMock.Setup(r => r.GetProductByIdAsync(5, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = 5,
                Name = "Producto real",
                SalePrice = 75.25m,
                IsActive = true,
                IsForSale = true,
                TrackStock = false
            });
        OrderItem? persistedItem = null;
        _checkoutRepoMock.Setup(r => r.AddItemsAsync(It.IsAny<AddOrderItemsCommand>()))
            .Callback<AddOrderItemsCommand>(command => persistedItem = command.Items.Single())
            .Returns(Task.CompletedTask);

        await _service.AddItemsToTableAsync(CompanyId, BranchId, 1,
        [
            new CreateTableItemDto
            {
                ProductId = 5,
                ProductName = "Nombre falso",
                Quantity = 1.5m,
                UnitPrice = 0.01m
            }
        ]);

        Assert.NotNull(persistedItem);
        Assert.Equal("Producto real", persistedItem!.ProductName);
        Assert.Equal(75.25m, persistedItem.UnitPrice);
        Assert.Equal(1.5m, persistedItem.Quantity);
        _checkoutRepoMock.Verify(r => r.AddItemsAsync(It.IsAny<AddOrderItemsCommand>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddItems_RejectsNonPositiveQuantity_WithoutItemOrOrderChanges(int quantity)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddItemsToTableAsync(CompanyId, BranchId, 1,
            [
                new CreateTableItemDto { ProductId = 5, Quantity = quantity }
            ]));

        _checkoutRepoMock.Verify(r => r.AddItemsAsync(It.IsAny<AddOrderItemsCommand>()), Times.Never);
    }

    private void SetupSaleableProduct(long productId)
    {
        _inventoryRepoMock.Setup(repository => repository.GetProductByIdAsync(productId, CompanyId))
            .ReturnsAsync(new Product
            {
                Id = productId,
                Name = "Producto",
                SalePrice = 100m,
                IsActive = true,
                IsForSale = true,
                TrackStock = false
            });
    }

}
