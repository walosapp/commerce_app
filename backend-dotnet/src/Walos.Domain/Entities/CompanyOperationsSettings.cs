namespace Walos.Domain.Entities;

public class CompanyOperationsSettings
{
    public long CompanyId { get; set; }
    public bool ManualDiscountEnabled { get; set; } = true;
    public decimal MaxDiscountPercent { get; set; } = 15;
    public decimal MaxDiscountAmount { get; set; } = 50000;
    public bool DiscountRequiresOverride { get; set; }
    public decimal DiscountOverrideThresholdPercent { get; set; } = 10;
}
