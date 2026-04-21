namespace Walos.Application.DTOs.Admin;

public class TenantResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string Currency { get; set; } = "COP";
    public string Language { get; set; } = "es";
    public bool IsActive { get; set; }
    public int BranchCount { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateTenantRequest
{
    public string? Name { get; set; }
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? Language { get; set; }
}

public class CreateTenantResult
{
    public TenantResponse Company { get; set; } = new();
    public long AdminUserId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public long BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
}
