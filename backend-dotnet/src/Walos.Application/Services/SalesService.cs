using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;
using Walos.Application.Storage;

namespace Walos.Application.Services;

public class SalesService : ISalesService
{
    private readonly ISalesRepository _salesRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IRecipeRepository _recipeRepo;
    private readonly ICreditRepository _creditRepo;
    private readonly ICashRegisterRepository _cashRegisterRepo;
    private readonly IOrderPaymentRepository _orderPaymentRepo;
    private readonly IUsersRepository _usersRepo;
    private readonly IRefundRepository _refundRepo;
    private readonly ICheckoutRepository _checkoutRepo;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<SalesService> _logger;

    public SalesService(
        ISalesRepository salesRepo,
        IInventoryRepository inventoryRepo,
        ICompanyRepository companyRepo,
        IRecipeRepository recipeRepo,
        ICreditRepository creditRepo,
        ICashRegisterRepository cashRegisterRepo,
        IOrderPaymentRepository orderPaymentRepo,
        IUsersRepository usersRepo,
        IRefundRepository refundRepo,
        ICheckoutRepository checkoutRepo,
        IFileStorage fileStorage,
        ILogger<SalesService> logger)
    {
        _salesRepo = salesRepo;
        _inventoryRepo = inventoryRepo;
        _companyRepo = companyRepo;
        _recipeRepo = recipeRepo;
        _creditRepo = creditRepo;
        _cashRegisterRepo = cashRegisterRepo;
        _orderPaymentRepo = orderPaymentRepo;
        _usersRepo = usersRepo;
        _refundRepo = refundRepo;
        _checkoutRepo = checkoutRepo;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<long?> ResolveBranchAsync(
        long companyId,
        long? tenantBranchId,
        long? requestedBranchId,
        bool required = false)
    {
        if (tenantBranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != tenantBranchId.Value)
                throw new ValidationException("La sucursal solicitada no corresponde a la sesión autenticada");

            return tenantBranchId.Value;
        }

        if (requestedBranchId.HasValue
            && !await _inventoryRepo.IsActiveBranchInCompanyAsync(requestedBranchId.Value, companyId))
        {
            throw new NotFoundException("Sucursal no encontrada");
        }

        if (required && !requestedBranchId.HasValue)
            throw new ValidationException("ID de sucursal requerido");

        return requestedBranchId;
    }

    public async Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId)
    {
        return await _salesRepo.GetActiveTablesAsync(companyId, branchId);
    }

    public async Task<CreateTableResult> CreateTableAsync(long companyId, long branchId, long userId, CreateTableRequest request)
    {
        var items = await ValidateAndNormalizeSaleItemsAsync(companyId, branchId, request.Items);

        var tableNumber = await _salesRepo.GetNextTableNumberAsync(companyId, branchId);

        var table = new SalesTable
        {
            CompanyId = companyId,
            BranchId = branchId,
            TableNumber = tableNumber,
            Name = request.Name ?? $"Mesa {tableNumber}",
            Status = "open",
            CreatedBy = userId
        };

        var createdTable = await _salesRepo.CreateTableAsync(table);

        var subtotal = items.Sum(item => item.Quantity * item.UnitPrice);
        var orderNumber = $"ORD-{createdTable.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        var order = new Order
        {
            CompanyId = companyId,
            BranchId = branchId,
            TableId = createdTable.Id,
            OrderNumber = orderNumber,
            Status = "pending",
            Subtotal = subtotal,
            Tax = 0,
            Total = subtotal,
            FinalTotalPaid = subtotal,
            CreatedBy = userId
        };

        await _salesRepo.CreateOrderAsync(order, items);

        createdTable.Items = items;
        createdTable.Total = subtotal;

        _logger.LogInformation("Mesa {TableNumber} creada con {ItemCount} productos, Total: {Total}",
            tableNumber, items.Count, subtotal);

        return new CreateTableResult
        {
            Table = createdTable,
            ItemCount = items.Count,
            Total = subtotal
        };
    }

    public async Task<InvoiceResult> InvoiceTableAsync(long companyId, long branchId, long userId, long tableId, InvoiceTableRequest request)
    {
        var result = await _checkoutRepo.ProcessAsync(new CheckoutCommand
        {
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            TableId = tableId,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            SubmittedFinalTotal = request.FinalTotalPaid,
            SplitCount = request.SplitCount,
            OverrideConfirmed = request.OverrideConfirmed,
            HasCredit = request.HasCredit,
            CreditAmountPaid = request.CreditAmountPaid,
            CreditCustomerName = request.CreditCustomerName,
            CreditNotes = request.CreditNotes,
            TipAmount = request.TipAmount,
            TipIncluded = request.TipIncluded,
            Payments = (request.Payments ?? [])
                .Select(payment => new CheckoutPayment(payment.Method, payment.Amount, payment.Reference))
                .ToList()
        });

        return new InvoiceResult
        {
            TableNumber = result.TableNumber,
            OrderNumber = result.OrderNumber,
            Subtotal = result.Subtotal,
            DiscountType = result.DiscountType,
            DiscountValue = result.DiscountValue,
            DiscountAmount = result.DiscountAmount,
            Total = result.AmountPaid,
            FinalTotalPaid = result.AmountPaid,
            TipAmount = result.TipAmount,
            SplitCount = result.SplitCount,
            Items = result.Items,
            Payments = result.Payments
                .Select(payment => new PaymentLineDto(payment.Method, payment.Amount, payment.Reference))
                .ToList(),
            InvoicedAt = result.InvoicedAt,
            CreditId = result.CreditId,
            CreditAmount = result.CreditAmount
        };
    }
    public async Task CancelTableAsync(long companyId, long tableId)
    {
        var table = await _salesRepo.GetTableByIdAsync(tableId, companyId)
            ?? throw new NotFoundException("Mesa no encontrada");

        var order = await _salesRepo.GetOrderByTableIdAsync(tableId, companyId);
        if (order != null)
            await _salesRepo.UpdateOrderStatusAsync(order.Id, companyId, "cancelled");

        await _salesRepo.UpdateTableStatusAsync(tableId, companyId, "cancelled");

        _logger.LogInformation("Mesa {TableNumber} cancelada", table.TableNumber);
    }

    public async Task CancelTableAsync(long companyId, long? branchId, long tableId)
    {
        var table = await _salesRepo.GetTableByIdAsync(tableId, companyId, branchId)
            ?? throw new NotFoundException("Mesa no encontrada");

        var order = await _salesRepo.GetOrderByTableIdAsync(tableId, companyId, branchId);
        if (order != null)
            await _salesRepo.UpdateOrderStatusAsync(order.Id, companyId, branchId, "cancelled");

        await _salesRepo.UpdateTableStatusAsync(tableId, companyId, branchId, "cancelled");

        _logger.LogInformation("Mesa {TableNumber} cancelada", table.TableNumber);
    }

    public async Task UpdateItemQuantityAsync(long companyId, long branchId, long itemId, UpdateItemQuantityRequest request)
    {
        if (request.Quantity <= 0)
            throw new ValidationException("La cantidad debe ser mayor que cero");

        var existingItem = await _salesRepo.GetOrderItemByIdAsync(itemId, companyId, branchId)
            ?? throw new NotFoundException("Item no encontrado");

        var order = await _salesRepo.GetOrderByIdAsync(existingItem.OrderId, companyId, branchId)
            ?? throw new NotFoundException("Orden no encontrada");

        var delta = request.Quantity - existingItem.Quantity;
        if (delta > 0)
        {
            await ValidateItemAvailabilityAsync(
                companyId,
                order.BranchId,
                new[] { (ProductId: existingItem.ProductId, Quantity: delta) });
        }

        await _checkoutRepo.UpdateItemQuantityAsync(new UpdateOrderItemQuantityCommand
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderItemId = itemId,
            Quantity = request.Quantity
        });
    }

    public async Task AddItemsToTableAsync(long companyId, long branchId, long tableId, List<CreateTableItemDto> items)
    {
        var normalizedItems = await ValidateAndNormalizeSaleItemsAsync(companyId, branchId, items);
        foreach (var item in normalizedItems)
        {
            item.CompanyId = companyId;
        }

        await _checkoutRepo.AddItemsAsync(new AddOrderItemsCommand
        {
            CompanyId = companyId,
            BranchId = branchId,
            TableId = tableId,
            Items = normalizedItems
        });

        _logger.LogInformation("Agregados {Count} productos a Mesa {TableId}", normalizedItems.Count, tableId);
    }

    public async Task RenameTableAsync(long companyId, long tableId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("El nombre no puede estar vacío");
        if (name.Length > 100)
            throw new ValidationException("El nombre no puede superar 100 caracteres");

        var table = await _salesRepo.GetTableByIdAsync(tableId, companyId)
            ?? throw new NotFoundException("Mesa no encontrada");

        if (table.Status != "open")
            throw new BusinessException("Solo se puede renombrar una mesa abierta");

        await _salesRepo.RenameTableAsync(tableId, companyId, name.Trim());
        _logger.LogInformation("Mesa {TableId} renombrada a '{Name}'", tableId, name.Trim());
    }

    public async Task RenameTableAsync(long companyId, long? branchId, long tableId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("El nombre no puede estar vacío");
        if (name.Length > 100)
            throw new ValidationException("El nombre no puede superar 100 caracteres");

        var table = await _salesRepo.GetTableByIdAsync(tableId, companyId, branchId)
            ?? throw new NotFoundException("Mesa no encontrada");

        if (table.Status != "open")
            throw new BusinessException("Solo se puede renombrar una mesa abierta");

        await _salesRepo.RenameTableAsync(tableId, companyId, branchId, name.Trim());
        _logger.LogInformation("Mesa {TableId} renombrada a '{Name}'", tableId, name.Trim());
    }

    public async Task<IEnumerable<OrderItem>> GetOrderItemsAsync(long companyId, long? branchId, long orderId)
    {
        _ = await _salesRepo.GetOrderByIdAsync(orderId, companyId, branchId)
            ?? throw new NotFoundException("Orden no encontrada");

        return await _salesRepo.GetOrderItemsAsync(orderId, companyId, branchId);
    }

    public async Task<ReceiptData> GetReceiptAsync(long companyId, long orderId)
        => await GetReceiptAsync(companyId, null, orderId);

    public async Task<ReceiptData> GetReceiptAsync(long companyId, long? branchId, long orderId)
    {
        var order = await _salesRepo.GetOrderByIdAsync(orderId, companyId, branchId)
            ?? throw new NotFoundException("Orden no encontrada");

        var company = await _companyRepo.GetCompanySettingsAsync(companyId);
        var items = (await _salesRepo.GetOrderItemsAsync(orderId, companyId, branchId)).ToList();
        var payments = (await _orderPaymentRepo.GetByOrderAsync(orderId, companyId)).ToList();
        var table = await _salesRepo.GetTableByIdAsync(order.TableId, companyId, branchId);

        var cashierName = "Cajero";
        if (order.CreatedBy.HasValue)
        {
            var user = await _usersRepo.GetByIdAsync(order.CreatedBy.Value, companyId);
            if (user != null) cashierName = $"{user.FirstName} {user.LastName}".Trim();
        }

        // Buscar credito asociado
        bool hasCredit = false;
        decimal? creditAmount = null;
        string? creditCustomerName = null;
        var credits = await _creditRepo.GetCreditsAsync(companyId, order.BranchId, null, order.OrderNumber);
        var credit = credits.FirstOrDefault();
        if (credit != null)
        {
            hasCredit = true;
            creditAmount = credit.CreditAmount;
            creditCustomerName = credit.CustomerName;
        }

        return new ReceiptData(
            CompanyName: company?.Name ?? "Empresa",
            CompanyLegalName: company?.LegalName,
            CompanyPhone: company?.Phone,
            CompanyLogoUrl: _fileStorage.ResolvePublicReference(company?.LogoUrl),
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            TableName: table?.Name ?? $"Mesa {table?.TableNumber ?? 0}",
            TableNumber: table?.TableNumber ?? 0,
            CreatedAt: order.CreatedAt,
            CashierName: cashierName,
            Items: items.Select(i => new ReceiptItemDto(i.ProductName, i.Quantity, i.UnitPrice, i.Subtotal)).ToList(),
            Subtotal: order.Subtotal,
            DiscountType: order.DiscountType,
            DiscountValue: order.DiscountValue,
            DiscountAmount: order.DiscountAmount,
            FinalTotalPaid: order.FinalTotalPaid,
            TipAmount: order.TipAmount,
            TipIncluded: order.TipIncluded,
            SplitCount: order.SplitReferenceCount,
            Payments: payments.Select(p => new ReceiptPaymentDto(p.Method, p.Amount, p.Reference)).ToList(),
            HasCredit: hasCredit,
            CreditAmount: creditAmount,
            CreditCustomerName: creditCustomerName
        );
    }

    public async Task<KitchenTicketData> GetKitchenTicketAsync(long companyId, long orderId)
        => await GetKitchenTicketAsync(companyId, null, orderId);

    public async Task<KitchenTicketData> GetKitchenTicketAsync(long companyId, long? branchId, long orderId)
    {
        var order = await _salesRepo.GetOrderByIdAsync(orderId, companyId, branchId)
            ?? throw new NotFoundException("Orden no encontrada");

        var items = (await _salesRepo.GetOrderItemsAsync(orderId, companyId, branchId)).ToList();
        var table = await _salesRepo.GetTableByIdAsync(order.TableId, companyId, branchId);

        var cashierName = "Cajero";
        if (order.CreatedBy.HasValue)
        {
            var user = await _usersRepo.GetByIdAsync(order.CreatedBy.Value, companyId);
            if (user != null) cashierName = $"{user.FirstName} {user.LastName}".Trim();
        }

        return new KitchenTicketData(
            TableName: table?.Name ?? $"Mesa {table?.TableNumber ?? 0}",
            TableNumber: table?.TableNumber ?? 0,
            OrderNumber: order.OrderNumber,
            CreatedAt: order.CreatedAt,
            CashierName: cashierName,
            Items: items.Select(i => new KitchenItemDto(i.ProductName, i.Quantity, i.Notes)).ToList()
        );
    }

    public async Task<(List<OrderDetailResponse> Items, int TotalCount)> SearchOrdersAsync(long companyId, OrderSearchRequest request)
    {
        var offset = (request.Page - 1) * request.Limit;
        var count = await _salesRepo.SearchOrdersCountAsync(companyId, request.BranchId,
            request.DateFrom, request.DateTo, request.Status, request.RefundStatus,
            request.PaymentMethod, request.Search, request.MinTotal, request.MaxTotal);

        var orders = await _salesRepo.SearchOrdersAsync(companyId, request.BranchId,
            request.DateFrom, request.DateTo, request.Status, request.RefundStatus,
            request.PaymentMethod, request.Search, request.MinTotal, request.MaxTotal,
            request.SortBy, request.SortDir, offset, request.Limit);

        var results = new List<OrderDetailResponse>();
        foreach (var o in orders)
        {
            results.Add(await MapToOrderDetail(companyId, request.BranchId, o, includeRefunds: false));
        }

        return (results, count);
    }

    public async Task<OrderDetailResponse> GetOrderDetailAsync(long companyId, long orderId)
        => await GetOrderDetailAsync(companyId, null, orderId);

    public async Task<OrderDetailResponse> GetOrderDetailAsync(long companyId, long? branchId, long orderId)
    {
        var order = await _salesRepo.GetOrderByIdAsync(orderId, companyId, branchId)
            ?? throw new NotFoundException("Orden no encontrada");

        var table = await _salesRepo.GetTableByIdAsync(order.TableId, companyId, branchId);
        order.TableName = table?.Name;
        order.TableNumber = table?.TableNumber;

        return await MapToOrderDetail(companyId, branchId, order, includeRefunds: true);
    }

    public async Task<byte[]> ExportOrdersCsvAsync(long companyId, OrderSearchRequest request)
    {
        var orders = await _salesRepo.SearchOrdersAsync(companyId, request.BranchId,
            request.DateFrom, request.DateTo, request.Status, request.RefundStatus,
            request.PaymentMethod, request.Search, request.MinTotal, request.MaxTotal,
            request.SortBy, request.SortDir, 0, 10000);

        using var ms = new System.IO.MemoryStream();
        using var sw = new System.IO.StreamWriter(ms, System.Text.Encoding.UTF8);

        sw.WriteLine("ID,Número Orden,Mesa,Estado,Subtotal,Descuento,Total Pagado,Propina,Método de Pago,Estado Devolución,Fecha");
        foreach (var o in orders)
        {
            sw.WriteLine($"{o.Id},{o.OrderNumber},{o.TableName ?? ""},{o.Status},{o.Subtotal:F2},{o.DiscountAmount:F2},{o.FinalTotalPaid:F2},{o.TipAmount:F2},{o.PaymentMethod ?? ""},{o.RefundStatus ?? ""},{o.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        sw.Flush();
        return ms.ToArray();
    }

    private async Task<OrderDetailResponse> MapToOrderDetail(long companyId, long? branchId, Order order, bool includeRefunds)
    {
        var items = (await _salesRepo.GetOrderItemsAsync(order.Id, companyId, branchId)).ToList();
        var payments = (await _orderPaymentRepo.GetByOrderAsync(order.Id, companyId)).ToList();

        var cashierName = "Cajero";
        if (order.CreatedBy.HasValue)
        {
            var user = await _usersRepo.GetByIdAsync(order.CreatedBy.Value, companyId);
            if (user != null) cashierName = $"{user.FirstName} {user.LastName}".Trim();
        }

        List<RefundSummaryDto>? refunds = null;
        if (includeRefunds)
        {
            var refundList = await _refundRepo.GetByOrderIdAsync(order.Id, companyId, order.BranchId);
            refunds = refundList.Select(r => new RefundSummaryDto(
                r.Id, r.RefundType, r.RefundAmount, r.Reason, r.Status, r.CreatedAt
            )).ToList();
        }

        var credits = await _creditRepo.GetCreditsAsync(companyId, order.BranchId, null, order.OrderNumber);
        var hasCredit = credits.Any();

        return new OrderDetailResponse(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            TableName: order.TableName ?? $"Mesa {order.TableNumber ?? 0}",
            TableNumber: order.TableNumber ?? 0,
            Status: order.Status,
            Subtotal: order.Subtotal,
            DiscountType: order.DiscountType,
            DiscountValue: order.DiscountValue,
            DiscountAmount: order.DiscountAmount,
            FinalTotalPaid: order.FinalTotalPaid,
            TipAmount: order.TipAmount,
            TipIncluded: order.TipIncluded,
            SplitReferenceCount: order.SplitReferenceCount,
            RefundStatus: order.RefundStatus,
            PaymentMethod: order.PaymentMethod,
            HasCredit: hasCredit,
            CreatedAt: order.CreatedAt,
            CashierName: cashierName,
            Items: items.Select(i => new ReceiptItemDto(i.ProductName, i.Quantity, i.UnitPrice, i.Subtotal)).ToList(),
            Payments: payments.Select(p => new ReceiptPaymentDto(p.Method, p.Amount, p.Reference)).ToList(),
            Refunds: refunds
        );
    }

    private async Task ValidateItemAvailabilityAsync(
        long companyId,
        long branchId,
        IEnumerable<(long ProductId, decimal Quantity)> requestedItems)
    {
        foreach (var requestedItem in requestedItems)
        {
            if (requestedItem.ProductId <= 0 || requestedItem.Quantity <= 0)
                throw new ValidationException("Todos los items deben tener producto y cantidad validos.");

            var product = await GetSaleableProductAsync(requestedItem.ProductId, companyId);
            await ValidateProductStockAsync(product, companyId, branchId, requestedItem.Quantity);
        }
    }

    private async Task<List<OrderItem>> ValidateAndNormalizeSaleItemsAsync(
        long companyId,
        long branchId,
        IEnumerable<CreateTableItemDto>? requestedItems)
    {
        var requestList = requestedItems?.ToList() ?? [];
        if (requestList.Count == 0)
            throw new ValidationException("Debe agregar al menos un producto");

        if (requestList.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
            throw new ValidationException("Todos los items deben tener producto y cantidad validos.");

        var products = new Dictionary<long, Product>();
        foreach (var group in requestList.GroupBy(item => item.ProductId))
        {
            var product = await GetSaleableProductAsync(group.Key, companyId);
            await ValidateProductStockAsync(product, companyId, branchId, group.Sum(item => item.Quantity));
            products.Add(product.Id, product);
        }

        return requestList.Select(item =>
        {
            var product = products[item.ProductId];
            return new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = Math.Round(product.SalePrice, 2)
            };
        }).ToList();
    }

    private async Task<Product> GetSaleableProductAsync(long productId, long companyId)
    {
        var product = await _inventoryRepo.GetProductByIdAsync(productId, companyId)
            ?? throw new ValidationException($"El producto {productId} no existe.");

        if (!product.IsActive)
            throw new ValidationException($"El producto {product.Name} no esta activo.");

        if (!product.IsForSale)
            throw new ValidationException($"El producto {product.Name} no esta disponible para venta.");

        if (product.SalePrice < 0)
            throw new ValidationException($"El producto {product.Name} tiene un precio de venta invalido.");

        return product;
    }

    private async Task ValidateProductStockAsync(
        Product product,
        long companyId,
        long branchId,
        decimal quantity)
    {
        if (!product.TrackStock)
            return;

        var stock = await _inventoryRepo.GetStockByProductAsync(branchId, product.Id, companyId);
        if (stock is null)
            throw new ValidationException($"El producto {product.Name} no tiene stock configurado en esta sucursal.");

        if (quantity > stock.AvailableQuantity)
            throw new ValidationException($"Stock insuficiente para {stock.ProductName ?? product.Name}. Disponible: {stock.AvailableQuantity:N2}. Comprometido: {stock.ReservedQuantity:N2}.");
    }

}

