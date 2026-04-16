namespace Walos.Domain.Entities;

public class DeliveryStatusHistory
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public long? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
