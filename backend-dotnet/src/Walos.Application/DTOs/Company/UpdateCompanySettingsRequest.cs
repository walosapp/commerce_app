namespace Walos.Application.DTOs.Company;

public class UpdateCompanySettingsRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string ThemePreference { get; set; } = "light";
    public string BusinessOpenTime { get; set; } = "00:00";
    public string BusinessCloseTime { get; set; } = "23:59";
}

public class UpdateCompanyOperationsSettingsRequest
{
    public bool ManualDiscountEnabled { get; set; } = true;
    public decimal MaxDiscountPercent { get; set; } = 15;
    public decimal MaxDiscountAmount { get; set; } = 50000;
    public bool DiscountRequiresOverride { get; set; }
    public decimal DiscountOverrideThresholdPercent { get; set; } = 10;
}
