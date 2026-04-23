namespace Walos.Domain.Entities.Platform;

public class PaymentMethod
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Provider { get; set; } = "wompi";
    public string? ProviderToken { get; set; }
    public string? Last4 { get; set; }
    public string? BankName { get; set; }
    public string? HolderName { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
