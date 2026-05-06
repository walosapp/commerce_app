using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Sales;
using Walos.Domain.Exceptions;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public interface IRefundService
{
    Task<RefundResponse> CreateRefundAsync(long companyId, long branchId, long userId, CreateRefundRequest request);
    Task<RefundResponse?> GetByIdAsync(long id, long companyId);
    Task<IEnumerable<RefundResponse>> GetByOrderIdAsync(long orderId, long companyId);
    Task<(IEnumerable<RefundResponse> Items, int TotalCount)> GetAllAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit);
}

public class RefundService : IRefundService
{
    private readonly IRefundRepository _refundRepo;
    private readonly ISalesRepository _salesRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IRecipeRepository _recipeRepo;
    private readonly ICreditRepository _creditRepo;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        IRefundRepository refundRepo,
        ISalesRepository salesRepo,
        IInventoryRepository inventoryRepo,
        IRecipeRepository recipeRepo,
        ICreditRepository creditRepo,
        ILogger<RefundService> logger)
    {
        _refundRepo = refundRepo;
        _salesRepo = salesRepo;
        _inventoryRepo = inventoryRepo;
        _recipeRepo = recipeRepo;
        _creditRepo = creditRepo;
        _logger = logger;
    }

    public async Task<RefundResponse> CreateRefundAsync(long companyId, long branchId, long userId, CreateRefundRequest request)
    {
        // Validaciones
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            throw new ValidationException("El motivo de la devolucion debe tener minimo 10 caracteres.");

        var order = await _salesRepo.GetOrderByIdAsync(request.OrderId, companyId)
            ?? throw new NotFoundException("Orden no encontrada.");

        if (order.Status != "completed")
            throw new ValidationException("Solo se pueden anular ordenes completadas.");

        if (order.RefundStatus == "full_refund")
            throw new ValidationException("Esta orden ya tiene una devolucion total.");

        var orderItems = (await _salesRepo.GetOrderItemsAsync(order.Id, companyId)).ToList();

        decimal refundAmount;
        var refundItems = new List<RefundItem>();

        if (request.RefundType == "full")
        {
            refundAmount = order.FinalTotalPaid;

            foreach (var item in orderItems)
            {
                refundItems.Add(new RefundItem
                {
                    OrderItemId = item.Id,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Quantity * item.UnitPrice
                });
            }
        }
        else if (request.RefundType == "partial")
        {
            if (request.Items == null || request.Items.Count == 0)
                throw new ValidationException("Debe especificar los items a devolver en una devolucion parcial.");

            refundAmount = 0;
            foreach (var reqItem in request.Items)
            {
                var orderItem = orderItems.FirstOrDefault(i => i.Id == reqItem.OrderItemId)
                    ?? throw new ValidationException($"Item de orden {reqItem.OrderItemId} no encontrado.");

                if (reqItem.Quantity <= 0 || reqItem.Quantity > orderItem.Quantity)
                    throw new ValidationException($"Cantidad invalida para {orderItem.ProductName}. Max: {orderItem.Quantity}");

                var subtotal = reqItem.Quantity * orderItem.UnitPrice;
                refundAmount += subtotal;

                refundItems.Add(new RefundItem
                {
                    OrderItemId = orderItem.Id,
                    Quantity = reqItem.Quantity,
                    UnitPrice = orderItem.UnitPrice,
                    Subtotal = subtotal
                });
            }
        }
        else
        {
            throw new ValidationException("Tipo de devolucion invalido. Use 'full' o 'partial'.");
        }

        // Crear refund
        var refund = await _refundRepo.CreateAsync(new Refund
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderId = order.Id,
            RefundType = request.RefundType,
            RefundAmount = Math.Round(refundAmount, 2),
            Reason = request.Reason.Trim(),
            Status = "completed",
            CreatedBy = userId
        });

        if (refundItems.Count > 0)
            await _refundRepo.CreateItemsAsync(refund.Id, refundItems);

        // Revertir stock
        await RevertStockAsync(companyId, branchId, userId, order, refundItems);

        // Actualizar estado de la orden
        var refundStatus = request.RefundType == "full" ? "full_refund" : "partial_refund";
        await _refundRepo.UpdateOrderRefundStatusAsync(order.Id, companyId, refundStatus);

        _logger.LogInformation("Devolucion {RefundType} creada para orden {OrderId}. Monto: {Amount}", request.RefundType, order.Id, refundAmount);

        return MapToResponse(refund, refundItems);
    }

    private async Task RevertStockAsync(long companyId, long branchId, long userId, Order order, List<RefundItem> refundItems)
    {
        var orderItems = (await _salesRepo.GetOrderItemsAsync(order.Id, companyId)).ToList();

        foreach (var ri in refundItems)
        {
            var orderItem = orderItems.FirstOrDefault(i => i.Id == ri.OrderItemId);
            if (orderItem == null) continue;

            try
            {
                var product = await _inventoryRepo.GetProductByIdAsync(orderItem.ProductId, companyId);
                var isPrepared = product?.ProductType == "prepared";

                if (!isPrepared)
                {
                    await _inventoryRepo.UpdateStockAsync(branchId, orderItem.ProductId, ri.Quantity, companyId);
                    await _inventoryRepo.CreateMovementAsync(new Movement
                    {
                        CompanyId = companyId,
                        BranchId = branchId,
                        ProductId = orderItem.ProductId,
                        MovementType = "refund",
                        Quantity = ri.Quantity,
                        UnitCost = orderItem.UnitPrice,
                        Notes = $"Devolucion - Orden {order.OrderNumber}",
                        CreatedBy = userId
                    });
                }
                else
                {
                    // Revertir insumos de receta
                    var ingredients = (await _recipeRepo.GetAllIngredientsForSaleAsync(
                        new[] { (orderItem.ProductId, ri.Quantity) }, companyId)).ToList();

                    foreach (var ing in ingredients)
                    {
                        await _inventoryRepo.UpdateStockAsync(branchId, ing.IngredientId, ing.Quantity, companyId);
                        await _inventoryRepo.CreateMovementAsync(new Movement
                        {
                            CompanyId = companyId,
                            BranchId = branchId,
                            ProductId = ing.IngredientId,
                            MovementType = "refund_recipe",
                            Quantity = ing.Quantity,
                            UnitCost = 0,
                            Notes = $"Devolucion receta - Orden {order.OrderNumber}",
                            CreatedBy = userId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error revirtiendo stock para item {ItemId} en devolucion", ri.OrderItemId);
            }
        }
    }

    public async Task<RefundResponse?> GetByIdAsync(long id, long companyId)
    {
        var refund = await _refundRepo.GetByIdAsync(id, companyId);
        return refund != null ? MapToResponse(refund, refund.Items ?? new()) : null;
    }

    public async Task<IEnumerable<RefundResponse>> GetByOrderIdAsync(long orderId, long companyId)
    {
        var refunds = await _refundRepo.GetByOrderIdAsync(orderId, companyId);
        return refunds.Select(r => MapToResponse(r, r.Items ?? new()));
    }

    public async Task<(IEnumerable<RefundResponse> Items, int TotalCount)> GetAllAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit)
    {
        var items = await _refundRepo.GetAllAsync(companyId, branchId, dateFrom, dateTo, page, limit);
        var count = await _refundRepo.GetCountAsync(companyId, branchId, dateFrom, dateTo);
        return (items.Select(r => MapToResponse(r, r.Items ?? new())), count);
    }

    private static RefundResponse MapToResponse(Refund r, List<RefundItem> items)
    {
        return new RefundResponse(
            r.Id, r.CompanyId, r.BranchId, r.OrderId,
            "", // OrderNumber filled by repo join when available
            r.RefundType, r.RefundAmount, r.Reason, r.Status,
            null, "", r.CreatedAt,
            items.Select(i => new RefundItemResponse(
                i.Id, i.OrderItemId, "", i.Quantity, i.UnitPrice, i.Subtotal
            )).ToList()
        );
    }
}
