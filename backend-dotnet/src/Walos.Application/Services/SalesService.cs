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
    private readonly ILogger<SalesService> _logger;

    public SalesService(
        ISalesRepository salesRepo,
        IInventoryRepository inventoryRepo,
        ICompanyRepository companyRepo,
        ILogger<SalesService> logger)
    {
        _salesRepo = salesRepo;
        _inventoryRepo = inventoryRepo;
        _companyRepo = companyRepo;
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

        foreach (var item in items)
        {
            try
            {
                await _inventoryRepo.UpdateStockAsync(branchId, item.ProductId, -item.Quantity, companyId);

                var movement = new Movement
                {
                    CompanyId = companyId,
                    BranchId = branchId,
                    ProductId = item.ProductId,
                    MovementType = "sale",
                    Quantity = item.Quantity,
                    UnitCost = item.UnitPrice,
                    Notes = $"Venta Mesa {table.TableNumber} - {order.OrderNumber}",
                    CreatedBy = userId
                };

                await _inventoryRepo.CreateMovementAsync(movement);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error descontando stock para producto {ProductId}", item.ProductId);
            }
        }

        await _salesRepo.UpdateOrderInvoiceSummaryAsync(
            order.Id,
            companyId,
            discountType == "none" ? null : discountType,
            discountType == "none" ? 0 : discountValue,
            discountAmount,
            finalTotalPaid,
            Math.Max(1, request.SplitCount));
        await _salesRepo.UpdateOrderStatusAsync(order.Id, companyId, "completed");
        await _salesRepo.UpdateTableStatusAsync(tableId, companyId, "invoiced");

        _logger.LogInformation("Mesa {TableNumber} facturada. Order: {OrderNumber}, Total: {Total}",
            table.TableNumber, order.OrderNumber, finalTotalPaid);

        return new InvoiceResult
        {
            TableNumber = table.TableNumber,
            OrderNumber = order.OrderNumber,
            Subtotal = subtotal,
            DiscountType = discountType,
            DiscountValue = discountType == "none" ? 0 : discountValue,
            DiscountAmount = discountAmount,
            Total = finalTotalPaid,
            FinalTotalPaid = finalTotalPaid,
            SplitCount = Math.Max(1, request.SplitCount),
            Items = items,
            InvoicedAt = DateTime.UtcNow
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
