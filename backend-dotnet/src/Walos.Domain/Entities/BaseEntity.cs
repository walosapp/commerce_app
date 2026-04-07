namespace Walos.Domain.Entities;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
