namespace Walos.Domain.Entities;

public class AiSession
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long UserId { get; set; }
    public string AgentType { get; set; } = "orchestrator";
    public string Context { get; set; } = "{}";
    public DateTime LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AiMessage> Messages { get; set; } = new();
}

public class AiMessage
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Metadata { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
