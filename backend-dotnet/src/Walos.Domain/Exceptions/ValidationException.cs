namespace Walos.Domain.Exceptions;

public class ValidationException : Exception
{
    public IReadOnlyList<ValidationDetail>? Details { get; }

    public ValidationException(string message, IReadOnlyList<ValidationDetail>? details = null)
        : base(message)
    {
        Details = details;
    }
}

public record ValidationDetail(string Field, string Message);
