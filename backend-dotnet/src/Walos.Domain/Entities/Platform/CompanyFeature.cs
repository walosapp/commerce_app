namespace Walos.Domain.Entities.Platform;

public class CompanyFeature
{
    public long CompanyId { get; set; }
    public string FeatureCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
