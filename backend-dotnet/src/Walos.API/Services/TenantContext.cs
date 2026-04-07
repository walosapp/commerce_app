using Walos.Domain.Interfaces;

namespace Walos.API.Services;

public class TenantContext : ITenantContext
{
    public long CompanyId { get; set; }
    public long UserId { get; set; }
    public long? BranchId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool IsDev => Role.Equals("dev", StringComparison.OrdinalIgnoreCase);
}
