namespace Walos.Domain.Entities;

public class AiInteraction : BaseEntity
{
    public long BranchId { get; set; }
    public long UserId { get; set; }
    public string? SessionId { get; set; }
    public string InteractionType { get; set; } = "text";
    public string UserInput { get; set; } = string.Empty;
    public string? AiResponse { get; set; }
    public string? AiAction { get; set; }
    public string? ProcessedData { get; set; }
    public string ActionStatus { get; set; } = "pending";
    public decimal? ConfidenceScore { get; set; }
    public string? AiModel { get; set; }
    public int? TokensUsed { get; set; }
    public bool ConfirmedByUser { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}
