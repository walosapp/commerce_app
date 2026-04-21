using FluentValidation;
using Walos.Application.DTOs.Inventory;

namespace Walos.Application.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MinimumLength(2).WithMessage("El nombre debe tener al menos 2 caracteres")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("El SKU es requerido")
            .MinimumLength(2).WithMessage("El SKU debe tener al menos 2 caracteres")
            .MaximumLength(50).WithMessage("El SKU no puede exceder 50 caracteres");

        RuleFor(x => x.Barcode)
            .MaximumLength(100).WithMessage("El código de barras no puede exceder 100 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Barcode));

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("La categoría es requerida");

        RuleFor(x => x.UnitId)
            .GreaterThan(0).WithMessage("La unidad de medida es requerida");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("El precio de costo debe ser mayor o igual a 0");

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0).WithMessage("El precio de venta debe ser mayor o igual a 0");

        RuleFor(x => x.ProductType)
            .Must(x => x is "simple" or "prepared" or "combo" or "service" or "supply")
            .WithMessage("El tipo de producto debe ser: simple, prepared, combo, service o supply");

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock mínimo debe ser mayor o igual a 0")
            .When(x => x.TrackStock);

        RuleFor(x => x.MaxStock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock máximo debe ser mayor o igual a 0")
            .When(x => x.TrackStock);

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0).WithMessage("El punto de reorden debe ser mayor o igual a 0")
            .When(x => x.TrackStock);

        RuleFor(x => x.ShelfLifeDays)
            .GreaterThan(0).WithMessage("Los días de vida útil deben ser mayores a 0")
            .When(x => x.IsPerishable && x.ShelfLifeDays.HasValue);
    }
}

