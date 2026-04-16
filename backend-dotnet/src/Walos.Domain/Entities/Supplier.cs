namespace Walos.Domain.Entities;

public class Supplier : BaseEntity
{
    public long? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }

    public List<SupplierProduct>? Products { get; set; }
}
