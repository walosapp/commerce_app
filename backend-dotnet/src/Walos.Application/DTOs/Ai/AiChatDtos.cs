namespace Walos.Application.DTOs.Ai;

public record AiChatRequest(string Message, long? SessionId = null);

public class AiChatResponse
{
    public long SessionId { get; set; }
    public string AgentType { get; set; } = "orchestrator";
    public string ResponseType { get; set; } = "text";
    public string Message { get; set; } = string.Empty;
    public object? Payload { get; set; }
}

public class AiChecklistItem
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool Checked { get; set; } = true;
}

public class AiChecklistPayload
{
    public List<AiChecklistItem> Items { get; set; } = new();
    public string NextAction { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public class AiWhatsAppPayload
{
    public string Phone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string WhatsAppUrl { get; set; } = string.Empty;
}

public class AiDeliveryPayload
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
