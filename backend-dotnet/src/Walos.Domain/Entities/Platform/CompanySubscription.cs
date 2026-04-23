namespace Walos.Domain.Entities.Platform;

public class CompanySubscription
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public decimal? CustomPrice { get; set; }
    public string BillingFrequency { get; set; } = "monthly";
    public DateOnly? NextBillingDate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? ServiceName { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal EffectivePrice => CustomPrice ?? BasePrice ?? 0;
}
