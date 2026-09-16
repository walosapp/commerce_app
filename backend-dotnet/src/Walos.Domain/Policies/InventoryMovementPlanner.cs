using Walos.Domain.Exceptions;

namespace Walos.Domain.Policies;

public enum InventoryMovementDirection
{
    Inbound,
    Outbound
}

public sealed record InventoryMovementPlan(
    InventoryMovementDirection Direction,
    long ProductId,
    string MovementType,
    decimal Quantity,
    decimal UnitCost,
    string ReferenceType,
    long ReferenceId,
    bool RequiresStock,
    string? Notes,
    decimal? StockAfter,
    long? SourceOrderItemId);

public static class InventoryMovementPlanner
{
    public const int InventoryQuantityDecimals = 3;

    public static InventoryMovementPlan Create(
        InventoryMovementDirection direction,
        long productId,
        string movementType,
        decimal quantity,
        decimal unitCost,
        string referenceType,
        long referenceId,
        bool requiresStock,
        string? notes = null,
        long? sourceOrderItemId = null)
    {
        if (productId <= 0)
            throw new ValidationException("Producto requerido para el movimiento");
        var normalizedQuantity = NormalizeQuantity(quantity);
        if (normalizedQuantity <= 0)
            throw new ValidationException("La cantidad del movimiento debe ser mayor que cero");
        if (unitCost < 0)
            throw new ValidationException("El costo del movimiento no puede ser negativo");
        if (string.IsNullOrWhiteSpace(movementType))
            throw new ValidationException("Tipo de movimiento requerido");
        if (string.IsNullOrWhiteSpace(referenceType) || referenceId <= 0)
            throw new ValidationException("Referencia de movimiento requerida");

        return new InventoryMovementPlan(
            direction,
            productId,
            movementType.Trim(),
            normalizedQuantity,
            PaymentPolicy.RoundMoney(unitCost),
            referenceType.Trim(),
            referenceId,
            requiresStock,
            notes?.Trim(),
            StockAfter: null,
            SourceOrderItemId: sourceOrderItemId);
    }

    public static InventoryMovementPlan WithStockAfter(InventoryMovementPlan plan, decimal stockAfter) =>
        plan with { StockAfter = stockAfter };

    public static decimal CalculateHistoricalRefundQuantity(
        decimal originalConsumption,
        decimal soldQuantity,
        decimal refundQuantity)
    {
        var normalizedOriginal = NormalizeQuantity(originalConsumption);
        var normalizedSold = NormalizeQuantity(soldQuantity);
        var normalizedRefund = NormalizeQuantity(refundQuantity);
        if (normalizedOriginal <= 0 || normalizedSold <= 0 || normalizedRefund <= 0)
            throw new ValidationException("Las cantidades historicas del refund deben ser mayores que cero");
        if (normalizedRefund > normalizedSold)
            throw new ValidationException("La cantidad devuelta no puede superar la cantidad vendida");

        return NormalizeQuantity(normalizedOriginal * normalizedRefund / normalizedSold);
    }

    public static decimal NormalizeQuantity(decimal quantity) =>
        Math.Round(quantity, InventoryQuantityDecimals, MidpointRounding.AwayFromZero);
}
