using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

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
        _logger = logger;
    }

    public async Task<IEnumerable<SalesTable>> GetActiveTablesAsync(long companyId, long branchId)
    {
        return await _salesRepo.GetActiveTablesAsync(companyId, branchId);
    }

    public async Task<CreateTableResult> CreateTableAsync(long companyId, long branchId, long userId, CreateTableRequest request)
    {
        if (request.Items.Count == 0)
            throw new ValidationException("Debe agregar al menos un producto");

        await ValidateItemAvailabilityAsync(
            companyId,
            branchId,
            request.Items
                .GroupBy(item => item.ProductId)
                .Select(group => (ProductId: group.Key, Quantity: group.Sum(item => item.Quantity))));

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

        var subtotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
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

        var items = request.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList();

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
        var table = await _salesRepo.GetTableByIdAsync(tableId, companyId)
            ?? throw new NotFoundException("Mesa no encontrada");

        if (table.Status != "open")
            throw new BusinessException("La mesa ya fue facturada o cancelada");

        var order = await _salesRepo.GetOrderByTableIdAsync(tableId, companyId)
            ?? throw new BusinessException("No hay orden asociada a esta mesa");

        var items = (await _salesRepo.GetOrderItemsAsync(order.Id, companyId)).ToList();
        var operations = await _companyRepo.GetCompanyOperationsSettingsAsync(companyId);
        var subtotal = order.Subtotal > 0 ? order.Subtotal : items.Sum(i => i.Quantity * i.UnitPrice);
        var discountType = (request.DiscountType ?? "none").Trim().ToLowerInvariant();
        var discountValue = Math.Round(request.DiscountValue, 2);
        var discountAmount = 0m;
        var discountPercent = 0m;

        if (discountType is not ("none" or "fixed" or "percentage"))
            throw new ValidationException("Tipo de descuento no permitido");

        if (discountType != "none")
        {
            if (operations is null)
                throw new BusinessException("No fue posible cargar reglas operativas");

            if (!operations.ManualDiscountEnabled)
                throw new BusinessException("El descuento manual esta deshabilitado en configuracion");

            if (discountType == "percentage")
            {
                if (discountValue < 0 || discountValue > operations.MaxDiscountPercent)
                    throw new ValidationException($"El descuento porcentual no puede superar {operations.MaxDiscountPercent:N2}%");

                discountPercent = discountValue;
                discountAmount = Math.Round(subtotal * (discountPercent / 100m), 2);
            }
            else
            {
                if (discountValue < 0 || discountValue > operations.MaxDiscountAmount)
                    throw new ValidationException($"El descuento fijo no puede superar {operations.MaxDiscountAmount:N0}");

                discountAmount = Math.Round(discountValue, 2);
                discountPercent = subtotal > 0 ? Math.Round((discountAmount / subtotal) * 100m, 2) : 0;
            }

            if (discountAmount > subtotal)
                throw new ValidationException("El descuento no puede ser mayor al subtotal");

            if (operations.DiscountRequiresOverride && discountPercent >= operations.DiscountOverrideThresholdPercent && !request.OverrideConfirmed)
                throw new ValidationException($"Este descuento requiere confirmacion adicional desde {operations.DiscountOverrideThresholdPercent:N2}%");
        }

        var finalTotalPaid = Math.Round(subtotal - discountAmount, 2);
        if (request.FinalTotalPaid > 0 && Math.Abs(request.FinalTotalPaid - finalTotalPaid) > 1)
            throw new ValidationException("El total final no coincide con el descuento aplicado");

        // Validar caja abierta (si la compania lo requiere)
        CashRegister? activeRegister = null;
        if (operations?.RequireCashRegister ?? true) // Default: requiere caja
        {
            activeRegister = await _cashRegisterRepo.GetActiveByUserAsync(companyId, branchId, userId);
            if (activeRegister == null)
                throw new BusinessException("Debes abrir una caja antes de facturar");
        }

        // Validar pagos: deben sumar al total cobrado (actualPaid)
        var actualPaid = (request.HasCredit && request.CreditAmountPaid >= 0 && request.CreditAmountPaid < finalTotalPaid)
            ? Math.Round(request.CreditAmountPaid, 2)
            : finalTotalPaid;

        if (request.Payments == null || request.Payments.Count == 0)
            throw new ValidationException("Debe especificar al menos un metodo de pago");

        var paymentsSum = request.Payments.Sum(p => p.Amount);
        var expectedPayment = actualPaid + (request.TipIncluded ? request.TipAmount : 0);
        if (Math.Abs(paymentsSum - expectedPayment) > 1)
            throw new ValidationException($"La suma de los pagos ({paymentsSum:N2}) no coincide con el total a cobrar ({expectedPayment:N2})");

        // Calcular descuentos de insumos para productos preparados (recetas)
        var soldTuples = items.Select(i => (i.ProductId, i.Quantity));
        var ingredientDeductions = (await _recipeRepo.GetAllIngredientsForSaleAsync(soldTuples, companyId)).ToList();

        foreach (var item in items)
        {
            try
            {
                var product = await _inventoryRepo.GetProductByIdAsync(item.ProductId, companyId);
                var isPrepared = product?.ProductType == "prepared";

                if (!isPrepared)
                {
                    // Producto simple o insumo: descuenta stock directo
                    await _inventoryRepo.UpdateStockAsync(branchId, item.ProductId, -item.Quantity, companyId);
                    await _inventoryRepo.CreateMovementAsync(new Movement
                    {
                        CompanyId = companyId, BranchId = branchId,
                        ProductId = item.ProductId, MovementType = "sale",
                        Quantity = item.Quantity, UnitCost = item.UnitPrice,
                        Notes = $"Venta Mesa {table.TableNumber} - {order.OrderNumber}",
                        CreatedBy = userId
                    });
                }
                else
                {
                    // Producto preparado: no descuenta su propio stock, sino sus insumos
                    _logger.LogInformation("Producto preparado {Name} vendido x{Qty} — descontando insumos", product!.Name, item.Quantity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error descontando stock para producto {ProductId}", item.ProductId);
            }
        }

        // Descontar insumos de la receta
        foreach (var deduction in ingredientDeductions)
        {
            try
            {
                await _inventoryRepo.UpdateStockAsync(branchId, deduction.IngredientId, -deduction.Quantity, companyId);
                await _inventoryRepo.CreateMovementAsync(new Movement
                {
                    CompanyId = companyId, BranchId = branchId,
                    ProductId = deduction.IngredientId, MovementType = "recipe_consumption",
                    Quantity = deduction.Quantity, UnitCost = 0,
                    Notes = $"Consumo receta - Venta Mesa {table.TableNumber} - {order.OrderNumber}",
                    CreatedBy = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error descontando insumo {IngredientId} de receta", deduction.IngredientId);
            }
        }

        await _salesRepo.UpdateOrderInvoiceSummaryAsync(
            order.Id,
            companyId,
            discountType == "none" ? null : discountType,
            discountType == "none" ? 0 : discountValue,
            discountAmount,
            actualPaid,
            Math.Max(1, request.SplitCount));
        await _salesRepo.UpdateOrderStatusAsync(order.Id, companyId, "completed");
        await _salesRepo.UpdateTableStatusAsync(tableId, companyId, "invoiced");

        // Registrar pagos y asociar a caja
        if (activeRegister != null)
        {
            // Registrar cada pago individual
            foreach (var payment in request.Payments)
            {
                await _orderPaymentRepo.CreateAsync(new OrderPayment
                {
                    CompanyId = companyId,
                    OrderId = order.Id,
                    Method = payment.Method.ToLowerInvariant(),
                    Amount = payment.Amount,
                    Reference = payment.Reference,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Calcular totales por método de pago para actualizar caja
            var totalCashSales = request.Payments
                .Where(p => p.Method.Equals("cash", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);
            var totalCardSales = request.Payments
                .Where(p => p.Method.Equals("card", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);
            var totalTransferSales = request.Payments
                .Where(p => p.Method.Equals("transfer", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);
            var totalNequiSales = request.Payments
                .Where(p => p.Method.Equals("nequi", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);
            var totalOtherSales = request.Payments
                .Where(p => !new[] { "cash", "card", "transfer", "nequi" }.Contains(p.Method.ToLowerInvariant()))
                .Sum(p => p.Amount);

            // Actualizar totales de la caja (incrementamos en 1 el orderCount)
            await _cashRegisterRepo.UpdateTotalsAsync(
                activeRegister.Id,
                companyId,
                actualPaid,
                totalCashSales,
                totalCardSales,
                totalTransferSales + totalNequiSales, // Agrupar transferencias digitales
                totalOtherSales,
                discountAmount,
                request.HasCredit ? Math.Round(finalTotalPaid - actualPaid, 2) : 0,
                request.TipAmount,
                1); // orderCount increment
        }

        // Crear registro de credito si aplica
        long? creditId = null;
        decimal? creditAmount = null;
        if (request.HasCredit && actualPaid < finalTotalPaid && actualPaid >= 0)
        {
            var remaining = Math.Round(finalTotalPaid - actualPaid, 2);
            var customerName = !string.IsNullOrWhiteSpace(request.CreditCustomerName)
                ? request.CreditCustomerName.Trim()
                : table.Name ?? $"Mesa {table.TableNumber}";

            var credit = await _creditRepo.CreateCreditAsync(new Domain.Entities.Credit
            {
                CompanyId = companyId,
                BranchId = branchId,
                OrderId = order.Id,
                CustomerName = customerName,
                OrderNumber = order.OrderNumber,
                OriginalTotal = finalTotalPaid,
                AmountPaid = actualPaid,
                CreditAmount = remaining,
                Status = "pending",
                Notes = request.CreditNotes,
                CreatedBy = userId
            });

            creditId = credit.Id;
            creditAmount = remaining;
            _logger.LogInformation("Credito {CreditId} creado por {Amount} para '{Customer}'",
                credit.Id, remaining, customerName);
        }

        _logger.LogInformation("Mesa {TableNumber} facturada. Order: {OrderNumber}, Total: {Total}",
            table.TableNumber, order.OrderNumber, actualPaid);

        return new InvoiceResult
        {
            TableNumber = table.TableNumber,
            OrderNumber = order.OrderNumber,
            Subtotal = subtotal,
            DiscountType = discountType,
            DiscountValue = discountType == "none" ? 0 : discountValue,
            DiscountAmount = discountAmount,
            Total = actualPaid,
            FinalTotalPaid = actualPaid,
            TipAmount = request.TipAmount,
            SplitCount = Math.Max(1, request.SplitCount),
            Items = items,
            Payments = request.Payments ?? new List<PaymentLineDto>(),
            InvoicedAt = DateTime.UtcNow,
            CreditId = creditId,
            CreditAmount = creditAmount
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

    public async Task UpdateItemQuantityAsync(long companyId, long branchId, long itemId, UpdateItemQuantityRequest request)
    {
        if (request.Quantity < 0)
            throw new ValidationException("La cantidad no puede ser negativa");

        var existingItem = await _salesRepo.GetOrderItemByIdAsync(itemId, companyId)
            ?? throw new NotFoundException("Item no encontrado");

        var order = await _salesRepo.GetOrderByIdAsync(existingItem.OrderId, companyId)
            ?? throw new NotFoundException("Orden no encontrada");

        var delta = request.Quantity - existingItem.Quantity;
        if (delta > 0)
        {
            await ValidateItemAvailabilityAsync(
                companyId,
                order.BranchId,
                new[] { (ProductId: existingItem.ProductId, Quantity: delta) });
        }

        if (request.Quantity == 0)
        {
            await _salesRepo.DeleteOrderItemAsync(itemId, companyId);
        }
        else
        {
            await _salesRepo.UpdateOrderItemQuantityAsync(itemId, companyId, request.Quantity);
        }

        if (request.OrderId > 0)
            await _salesRepo.RecalculateOrderTotalAsync(request.OrderId, companyId);
    }

    public async Task AddItemsToTableAsync(long companyId, long tableId, List<CreateTableItemDto> items)
    {
        var table = await _salesRepo.GetTableByIdAsync(tableId, companyId)
            ?? throw new NotFoundException("Mesa no encontrada");

        if (table.Status != "open")
            throw new BusinessException("La mesa no esta abierta");

        var order = await _salesRepo.GetOrderByTableIdAsync(tableId, companyId)
            ?? throw new BusinessException("No hay orden asociada a esta mesa");

        await ValidateItemAvailabilityAsync(
            companyId,
            order.BranchId,
            items
                .GroupBy(item => item.ProductId)
                .Select(group => (ProductId: group.Key, Quantity: group.Sum(item => item.Quantity))));

        var existingItems = (await _salesRepo.GetOrderItemsAsync(order.Id, companyId)).ToList();

        foreach (var item in items)
        {
            var existingItem = existingItems.FirstOrDefault(existing => existing.ProductId == item.ProductId);

            if (existingItem is not null)
            {
                await _salesRepo.UpdateOrderItemQuantityAsync(existingItem.Id, companyId, existingItem.Quantity + item.Quantity);
                existingItem.Quantity += item.Quantity;
                continue;
            }

            var newItem = new OrderItem
            {
                CompanyId = companyId,
                OrderId = order.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            };

            await _salesRepo.AddOrderItemAsync(newItem);
            existingItems.Add(newItem);
        }

        await _salesRepo.RecalculateOrderTotalAsync(order.Id, companyId);

        _logger.LogInformation("Agregados {Count} productos a Mesa {TableNumber}", items.Count, table.TableNumber);
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

    public async Task<ReceiptData> GetReceiptAsync(long companyId, long orderId)
    {
        var order = await _salesRepo.GetOrderByIdAsync(orderId, companyId)
            ?? throw new NotFoundException("Orden no encontrada");

        var company = await _companyRepo.GetCompanySettingsAsync(companyId);
        var items = (await _salesRepo.GetOrderItemsAsync(orderId, companyId)).ToList();
        var payments = (await _orderPaymentRepo.GetByOrderAsync(orderId, companyId)).ToList();
        var table = await _salesRepo.GetTableByIdAsync(order.TableId, companyId);

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
        var credits = await _creditRepo.GetCreditsAsync(companyId, null, order.OrderNumber);
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
            CompanyLogoUrl: company?.LogoUrl,
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
    {
        var order = await _salesRepo.GetOrderByIdAsync(orderId, companyId)
            ?? throw new NotFoundException("Orden no encontrada");

        var items = (await _salesRepo.GetOrderItemsAsync(orderId, companyId)).ToList();
        var table = await _salesRepo.GetTableByIdAsync(order.TableId, companyId);

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
            results.Add(await MapToOrderDetail(companyId, o, includeRefunds: false));
        }

        return (results, count);
    }

    public async Task<OrderDetailResponse> GetOrderDetailAsync(long companyId, long orderId)
    {
        var order = await _salesRepo.GetOrderByIdAsync(orderId, companyId)
            ?? throw new NotFoundException("Orden no encontrada");

        var table = await _salesRepo.GetTableByIdAsync(order.TableId, companyId);
        order.TableName = table?.Name;
        order.TableNumber = table?.TableNumber;

        return await MapToOrderDetail(companyId, order, includeRefunds: true);
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

    private async Task<OrderDetailResponse> MapToOrderDetail(long companyId, Order order, bool includeRefunds)
    {
        var items = (await _salesRepo.GetOrderItemsAsync(order.Id, companyId)).ToList();
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
            var refundList = await _refundRepo.GetByOrderIdAsync(order.Id, companyId);
            refunds = refundList.Select(r => new RefundSummaryDto(
                r.Id, r.RefundType, r.RefundAmount, r.Reason, r.Status, r.CreatedAt
            )).ToList();
        }

        var credits = await _creditRepo.GetCreditsAsync(companyId, null, order.OrderNumber);
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
            var product = await _inventoryRepo.GetProductByIdAsync(requestedItem.ProductId, companyId)
                ?? throw new ValidationException($"El producto {requestedItem.ProductId} no existe.");

            if (!product.IsActive)
                throw new ValidationException($"El producto {product.Name} no esta activo.");

            if (!product.TrackStock)
                continue;

            var stock = await _inventoryRepo.GetStockByProductAsync(branchId, requestedItem.ProductId, companyId);
            if (stock is null)
                throw new ValidationException($"El producto {product.Name} no tiene stock configurado en esta sucursal.");

            if (requestedItem.Quantity > stock.AvailableQuantity)
                throw new ValidationException($"Stock insuficiente para {stock.ProductName ?? product.Name}. Disponible: {stock.AvailableQuantity:N2}. Comprometido: {stock.ReservedQuantity:N2}.");
        }
    }
}
