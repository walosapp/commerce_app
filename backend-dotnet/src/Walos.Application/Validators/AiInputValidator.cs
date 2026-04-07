using FluentValidation;
using Walos.Application.DTOs.Inventory;

namespace Walos.Application.Validators;

public class AiInputValidator : AbstractValidator<AiInputRequest>
{
    public AiInputValidator()
    {
        RuleFor(x => x.UserInput)
            .NotEmpty().WithMessage("La entrada de usuario es requerida")
            .MinimumLength(1).WithMessage("La entrada debe tener al menos 1 carácter")
            .MaximumLength(5000).WithMessage("La entrada no puede exceder 5000 caracteres");

        RuleFor(x => x.InputType)
            .Must(x => x is "text" or "voice")
            .WithMessage("El tipo de entrada debe ser 'text' o 'voice'");

        RuleFor(x => x.SessionId)
            .Must(x => Guid.TryParse(x, out _))
            .WithMessage("El sessionId debe ser un UUID válido")
            .When(x => !string.IsNullOrEmpty(x.SessionId));
    }
}
