namespace Walos.Application.DTOs.Inventory;

public class AiInputRequest
{
    public string UserInput { get; set; } = string.Empty;
    public string InputType { get; set; } = "text";
    public string? SessionId { get; set; }
}
