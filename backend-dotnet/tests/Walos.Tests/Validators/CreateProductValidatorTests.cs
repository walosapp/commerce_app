using FluentValidation.TestHelper;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Validators;

namespace Walos.Tests.Validators;

public class CreateProductValidatorTests
{
    private readonly CreateProductValidator _validator = new();

    [Fact]
    public void Should_Pass_When_ValidRequest()
    {
        var model = new CreateProductRequest
        {
            Name = "Ron Abuelo 12",
            Sku = "RON-AB-12",
            CategoryId = 1,
            UnitId = 1,
            CostPrice = 15.00m,
            SalePrice = 25.00m,
            MinStock = 5,
            MaxStock = 100,
            ReorderPoint = 10
        };

        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_NameEmpty()
    {
        var model = new CreateProductRequest { Name = "", Sku = "SKU-1", CategoryId = 1, UnitId = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Fail_When_SkuEmpty()
    {
        var model = new CreateProductRequest { Name = "Producto", Sku = "", CategoryId = 1, UnitId = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Fact]
    public void Should_Fail_When_CategoryIdZero()
    {
        var model = new CreateProductRequest { Name = "Producto", Sku = "SKU-1", CategoryId = 0, UnitId = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Fact]
    public void Should_Fail_When_UnitIdZero()
    {
        var model = new CreateProductRequest { Name = "Producto", Sku = "SKU-1", CategoryId = 1, UnitId = 0 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.UnitId);
    }

    [Fact]
    public void Should_Fail_When_CostPriceNegative()
    {
        var model = new CreateProductRequest { Name = "Producto", Sku = "SKU-1", CategoryId = 1, UnitId = 1, CostPrice = -1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CostPrice);
    }

    [Fact]
    public void Should_Fail_When_NameTooLong()
    {
        var model = new CreateProductRequest { Name = new string('A', 201), Sku = "SKU-1", CategoryId = 1, UnitId = 1 };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
