namespace Walos.Domain.Entities;

public class Alert : BaseEntity
{
    public long? BranchId { get; set; }
    public long? ProductId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string Status { get; set; } = "active";

    // Navigation properties (read-only, from JOINs)
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
}
