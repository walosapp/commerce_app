using Walos.Domain.Exceptions;

namespace Walos.Domain.Policies;

public sealed record SaleItemSnapshot(
    long ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public static class SaleItemPolicy
{
    // sales.order_items.quantity is currently NUMERIC(18,2).
    public const int CurrentQuantityDecimals = 2;
    public const int TargetQuantityDecimals = 3;

    public static bool IsQuantitySupported(decimal quantity) =>
        quantity > 0 &&
        Math.Round(quantity, CurrentQuantityDecimals, MidpointRounding.AwayFromZero) == quantity;

    public static decimal NormalizeQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new ValidationException("La cantidad debe ser mayor que cero");
        if (!IsQuantitySupported(quantity))
            throw new ValidationException(
                $"La cantidad admite temporalmente hasta {CurrentQuantityDecimals} decimales");

        return quantity;
    }

    public static SaleItemSnapshot CreateSnapshot(
        long productId,
        string productName,
        decimal quantity,
        decimal unitPrice)
    {
        if (productId <= 0)
            throw new ValidationException("Producto requerido");
        if (string.IsNullOrWhiteSpace(productName))
            throw new ValidationException("Nombre de producto requerido");
        if (unitPrice < 0)
            throw new ValidationException("El precio de venta no puede ser negativo");

        var normalizedQuantity = NormalizeQuantity(quantity);
        var normalizedPrice = PaymentPolicy.RoundMoney(unitPrice);
        return new SaleItemSnapshot(
            productId,
            productName.Trim(),
            normalizedQuantity,
            normalizedPrice,
            PaymentPolicy.RoundMoney(normalizedQuantity * normalizedPrice));
    }

    public static decimal CalculateSubtotal(decimal quantity, decimal unitPrice) =>
        CreateSnapshot(1, "snapshot", quantity, unitPrice).Subtotal;

    public static void ValidatePreparedRecipe(string? productType, int validIngredientCount)
    {
        if (string.Equals(productType?.Trim(), "prepared", StringComparison.OrdinalIgnoreCase) &&
            validIngredientCount <= 0)
        {
            throw new ValidationException("El producto preparado no tiene una receta valida");
        }
    }
}
