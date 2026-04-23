namespace Walos.Domain.Entities.Platform;

public class CompanyAiSettings
{
    public long CompanyId { get; set; }
    public bool AiKeyManaged { get; set; } = true;
    public string? AiProvider { get; set; } = "openai";
    public bool HasCustomKey { get; set; }
    public long AiTokensUsed { get; set; }
    public DateTime? AiTokensResetAt { get; set; }
    public decimal AiEstimatedCost { get; set; }
}
