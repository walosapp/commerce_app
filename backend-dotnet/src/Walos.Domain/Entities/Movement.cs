namespace Walos.Domain.Entities;

public class Movement : BaseEntity
{
    public long BranchId { get; set; }
    public long ProductId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
    public bool CreatedByAi { get; set; }
    public decimal? AiConfidence { get; set; }
    public string? AiMetadata { get; set; }
    public long? CreatedBy { get; set; }
}
