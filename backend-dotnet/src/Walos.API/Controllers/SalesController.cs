using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly ISalesRepository _salesRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly ITenantContext _tenant;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        ISalesRepository salesRepo,
        IInventoryRepository inventoryRepo,
        ICompanyRepository companyRepo,
        ITenantContext tenant,
        ILogger<SalesController> logger)
    {
        _salesRepo = salesRepo;
        _inventoryRepo = inventoryRepo;
        _companyRepo = companyRepo;
        _tenant = tenant;
        _logger = logger;
    }

    private async Task<string?> ValidateItemAvailabilityAsync(
        long companyId,
        long branchId,
        IEnumerable<(long ProductId, decimal Quantity)> requestedItems)
    {
        foreach (var requestedItem in requestedItems)
        {
            var product = await _inventoryRepo.GetProductByIdAsync(requestedItem.ProductId, companyId);
            if (product is null)
                return $"El producto {requestedItem.ProductId} no existe.";

            if (!product.IsActive)
                return $"El producto {product.Name} no esta activo.";

            if (!product.TrackStock)
                continue;

            var stock = await _inventoryRepo.GetStockByProductAsync(branchId, requestedItem.ProductId, companyId);
            if (stock is null)
            {
                return $"El producto {product.Name} no tiene stock configurado en esta sucursal.";
            }

            if (requestedItem.Quantity > stock.AvailableQuantity)
            {
                return $"Stock insuficiente para {stock.ProductName ?? product.Name}. Disponible: {stock.AvailableQuantity:N2}. Comprometido: {stock.ReservedQuantity:N2}.";
            }
        }

        return null;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables([FromQuery] long? branchId)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;

        if (branch is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var tables = await _salesRepo.GetActiveTablesAsync(companyId, branch.Value);
        var list = tables.ToList();

        return Ok(ApiResponse<List<SalesTable>>.Ok(list, count: list.Count));
    }

    [HttpPost("tables")]
    public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branchId = _tenant.BranchId;

        if (branchId is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        if (request.Items.Count == 0)
            return BadRequest(ApiResponse.Fail("Debe agregar al menos un producto"));

        var availabilityError = await ValidateItemAvailabilityAsync(
            companyId,
            branchId.Value,
            request.Items
                .GroupBy(item => item.ProductId)
                .Select(group => (ProductId: group.Key, Quantity: group.Sum(item => item.Quantity))));

        if (availabilityError is not null)
            return BadRequest(ApiResponse.Fail(availabilityError));

        var tableNumber = await _salesRepo.GetNextTableNumberAsync(companyId, branchId.Value);

        var table = new SalesTable
        {
            CompanyId = companyId,
            BranchId = branchId.Value,
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
            BranchId = branchId.Value,
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

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<SalesTable>.Ok(createdTable, "Mesa creada exitosamente"));
    }

    [HttpPost("tables/{id:long}/invoice")]
    public async Task<IActionResult> InvoiceTable(long id, [FromBody] InvoiceTableRequest? request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branchId = _tenant.BranchId;
        request ??= new InvoiceTableRequest();

        var table = await _salesRepo.GetTableByIdAsync(id, companyId);
        if (table is null)
            return NotFound(ApiResponse.Fail("Mesa no encontrada"));

        if (table.Status != "open")
            return BadRequest(ApiResponse.Fail("La mesa ya fue facturada o cancelada"));

        var order = await _salesRepo.GetOrderByTableIdAsync(id, companyId);
        if (order is null)
            return BadRequest(ApiResponse.Fail("No hay orden asociada a esta mesa"));

        var items = (await _salesRepo.GetOrderItemsAsync(order.Id)).ToList();
        var operations = await _companyRepo.GetCompanyOperationsSettingsAsync(companyId);
        var subtotal = order.Subtotal > 0 ? order.Subtotal : items.Sum(i => i.Quantity * i.UnitPrice);
        var discountType = (request.DiscountType ?? "none").Trim().ToLowerInvariant();
        var discountValue = Math.Round(request.DiscountValue, 2);
        var discountAmount = 0m;
        var discountPercent = 0m;

        if (discountType is not ("none" or "fixed" or "percentage"))
            return BadRequest(ApiResponse.Fail("Tipo de descuento no permitido"));

        if (discountType != "none")
        {
            if (operations is null)
                return BadRequest(ApiResponse.Fail("No fue posible cargar reglas operativas"));

            if (!operations.ManualDiscountEnabled)
                return BadRequest(ApiResponse.Fail("El descuento manual esta deshabilitado en configuracion"));

            if (discountType == "percentage")
            {
                if (discountValue < 0 || discountValue > operations.MaxDiscountPercent)
                    return BadRequest(ApiResponse.Fail($"El descuento porcentual no puede superar {operations.MaxDiscountPercent:N2}%"));

                discountPercent = discountValue;
                discountAmount = Math.Round(subtotal * (discountPercent / 100m), 2);
            }
            else
            {
                if (discountValue < 0 || discountValue > operations.MaxDiscountAmount)
                    return BadRequest(ApiResponse.Fail($"El descuento fijo no puede superar {operations.MaxDiscountAmount:N0}"));

                discountAmount = Math.Round(discountValue, 2);
                discountPercent = subtotal > 0 ? Math.Round((discountAmount / subtotal) * 100m, 2) : 0;
            }

            if (discountAmount > subtotal)
                return BadRequest(ApiResponse.Fail("El descuento no puede ser mayor al subtotal"));

            if (operations.DiscountRequiresOverride && discountPercent >= operations.DiscountOverrideThresholdPercent && !request.OverrideConfirmed)
                return BadRequest(ApiResponse.Fail($"Este descuento requiere confirmacion adicional desde {operations.DiscountOverrideThresholdPercent:N2}%"));
        }

        var finalTotalPaid = Math.Round(subtotal - discountAmount, 2);
        if (request.FinalTotalPaid > 0 && Math.Abs(request.FinalTotalPaid - finalTotalPaid) > 1)
            return BadRequest(ApiResponse.Fail("El total final no coincide con el descuento aplicado"));

        if (branchId.HasValue)
        {
            foreach (var item in items)
            {
                try
                {
                    await _inventoryRepo.UpdateStockAsync(branchId.Value, item.ProductId, -item.Quantity, companyId);

                    var movement = new Movement
                    {
                        CompanyId = companyId,
                        BranchId = branchId.Value,
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
        }

        await _salesRepo.UpdateOrderInvoiceSummaryAsync(
            order.Id,
            discountType == "none" ? null : discountType,
            discountType == "none" ? 0 : discountValue,
            discountAmount,
            finalTotalPaid,
            Math.Max(1, request.SplitCount));
        await _salesRepo.UpdateOrderStatusAsync(order.Id, "completed");
        await _salesRepo.UpdateTableStatusAsync(id, companyId, "invoiced");

        _logger.LogInformation("Mesa {TableNumber} facturada. Order: {OrderNumber}, Total: {Total}",
            table.TableNumber, order.OrderNumber, finalTotalPaid);

        return Ok(ApiResponse<object>.Ok(new
        {
            table.TableNumber,
            order.OrderNumber,
            Subtotal = subtotal,
            DiscountType = discountType,
            DiscountValue = discountType == "none" ? 0 : discountValue,
            DiscountAmount = discountAmount,
            Total = finalTotalPaid,
            FinalTotalPaid = finalTotalPaid,
            SplitCount = Math.Max(1, request.SplitCount),
            Items = items,
            InvoicedAt = DateTime.UtcNow
        }, "Mesa facturada exitosamente"));
    }

    [HttpDelete("tables/{id:long}")]
    public async Task<IActionResult> CancelTable(long id)
    {
        var companyId = _tenant.CompanyId;

        var table = await _salesRepo.GetTableByIdAsync(id, companyId);
        if (table is null)
            return NotFound(ApiResponse.Fail("Mesa no encontrada"));

        var order = await _salesRepo.GetOrderByTableIdAsync(id, companyId);
        if (order != null)
            await _salesRepo.UpdateOrderStatusAsync(order.Id, "cancelled");

        await _salesRepo.UpdateTableStatusAsync(id, companyId, "cancelled");

        _logger.LogInformation("Mesa {TableNumber} cancelada", table.TableNumber);

        return Ok(ApiResponse.Ok("Mesa cancelada"));
    }

    [HttpPatch("items/{itemId:long}/quantity")]
    public async Task<IActionResult> UpdateItemQuantity(long itemId, [FromBody] UpdateItemQuantityRequest request)
    {
        var companyId = _tenant.CompanyId;

        if (request.Quantity < 0)
            return BadRequest(ApiResponse.Fail("La cantidad no puede ser negativa"));

        var existingItem = await _salesRepo.GetOrderItemByIdAsync(itemId);
        if (existingItem is null)
            return NotFound(ApiResponse.Fail("Item no encontrado"));

        var order = await _salesRepo.GetOrderByIdAsync(existingItem.OrderId, companyId);
        if (order is null)
            return NotFound(ApiResponse.Fail("Orden no encontrada"));

        var delta = request.Quantity - existingItem.Quantity;
        if (delta > 0)
        {
            var availabilityError = await ValidateItemAvailabilityAsync(
                companyId,
                order.BranchId,
                new[] { (ProductId: existingItem.ProductId, Quantity: delta) });

            if (availabilityError is not null)
                return BadRequest(ApiResponse.Fail(availabilityError));
        }

        if (request.Quantity == 0)
        {
            await _salesRepo.DeleteOrderItemAsync(itemId);
        }
        else
        {
            await _salesRepo.UpdateOrderItemQuantityAsync(itemId, request.Quantity);
        }

        if (request.OrderId > 0)
            await _salesRepo.RecalculateOrderTotalAsync(request.OrderId);

        return Ok(ApiResponse.Ok("Cantidad actualizada"));
    }

    [HttpPost("tables/{id:long}/items")]
    public async Task<IActionResult> AddItemsToTable(long id, [FromBody] List<CreateTableItemDto> items)
    {
        var companyId = _tenant.CompanyId;

        var table = await _salesRepo.GetTableByIdAsync(id, companyId);
        if (table is null)
            return NotFound(ApiResponse.Fail("Mesa no encontrada"));

        if (table.Status != "open")
            return BadRequest(ApiResponse.Fail("La mesa no esta abierta"));

        var order = await _salesRepo.GetOrderByTableIdAsync(id, companyId);
        if (order is null)
            return BadRequest(ApiResponse.Fail("No hay orden asociada a esta mesa"));

        var availabilityError = await ValidateItemAvailabilityAsync(
            companyId,
            order.BranchId,
            items
                .GroupBy(item => item.ProductId)
                .Select(group => (ProductId: group.Key, Quantity: group.Sum(item => item.Quantity))));

        if (availabilityError is not null)
            return BadRequest(ApiResponse.Fail(availabilityError));

        var existingItems = (await _salesRepo.GetOrderItemsAsync(order.Id)).ToList();

        foreach (var item in items)
        {
            var existingItem = existingItems.FirstOrDefault(existing => existing.ProductId == item.ProductId);

            if (existingItem is not null)
            {
                await _salesRepo.UpdateOrderItemQuantityAsync(existingItem.Id, existingItem.Quantity + item.Quantity);
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

        await _salesRepo.RecalculateOrderTotalAsync(order.Id);

        _logger.LogInformation("Agregados {Count} productos a Mesa {TableNumber}", items.Count, table.TableNumber);

        return Ok(ApiResponse.Ok("Productos agregados exitosamente"));
    }
}
