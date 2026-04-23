namespace Walos.Domain.Entities;

public class CompanySettings
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "MXN";
    public string Timezone { get; set; } = "America/Mexico_City";
    public string Language { get; set; } = "es";
    public string ThemePreference { get; set; } = "light";
    public string? PrimaryColor { get; set; }
    public bool ManualDiscountEnabled { get; set; } = true;
    public decimal MaxDiscountPercent { get; set; } = 15;
    public decimal MaxDiscountAmount { get; set; } = 50000;
    public bool DiscountRequiresOverride { get; set; }
    public decimal DiscountOverrideThresholdPercent { get; set; } = 10;
    public TimeSpan BusinessOpenTime { get; set; } = TimeSpan.Zero;
    public TimeSpan BusinessCloseTime { get; set; } = new TimeSpan(23, 59, 59);
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
