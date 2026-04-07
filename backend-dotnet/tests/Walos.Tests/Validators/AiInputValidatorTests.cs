using FluentValidation.TestHelper;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Validators;

namespace Walos.Tests.Validators;

public class AiInputValidatorTests
{
    private readonly AiInputValidator _validator = new();

    [Fact]
    public void Should_Pass_When_ValidRequest()
    {
        var model = new AiInputRequest
        {
            UserInput = "Llegaron 10 cajas de Ron Abuelo a $15 cada una",
            InputType = "text"
        };

        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_UserInputEmpty()
    {
        var model = new AiInputRequest { UserInput = "", InputType = "text" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.UserInput);
    }

    [Fact]
    public void Should_Fail_When_InvalidInputType()
    {
        var model = new AiInputRequest { UserInput = "test", InputType = "image" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.InputType);
    }

    [Fact]
    public void Should_Pass_When_ValidSessionId()
    {
        var model = new AiInputRequest
        {
            UserInput = "test",
            InputType = "text",
            SessionId = Guid.NewGuid().ToString()
        };

        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_InvalidSessionId()
    {
        var model = new AiInputRequest
        {
            UserInput = "test",
            InputType = "text",
            SessionId = "not-a-uuid"
        };

        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }
}
