namespace Walos.Application.DTOs.Inventory;

public sealed record SaleCatalogProductResponse(
    long ProductId,
    string ProductName,
    string? Sku,
    string? Category,
    string? Unit,
    decimal SalePrice,
    string? ImageUrl,
    string? ProductType,
    bool HasValidRecipe,
    bool TrackStock,
    bool IsForSale,
    bool IsPerishable,
    decimal AvailableQuantity,
    bool IsConfiguredForSale);
