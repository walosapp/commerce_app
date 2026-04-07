namespace Walos.Domain.Interfaces;

public interface ITenantContext
{
    long CompanyId { get; }
    long UserId { get; }
    long? BranchId { get; }
    string Role { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
    bool IsDev { get; }
}
