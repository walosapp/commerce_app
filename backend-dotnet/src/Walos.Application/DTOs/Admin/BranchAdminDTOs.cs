namespace Walos.Application.DTOs.Admin;

public sealed record BranchAdminResponse(
    long Id,
    long CompanyId,
    string Name,
    string Code,
    string BranchType,
    string? Email,
    string? Phone,
    string Address,
    string City,
    string? State,
    string Country,
    string? PostalCode,
    int? MaxTables,
    int? MaxCapacity,
    bool IsMain,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateBranchAdminRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string BranchType { get; set; } = "general";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Country { get; set; } = "CO";
    public string? PostalCode { get; set; }
    public int? MaxTables { get; set; }
    public int? MaxCapacity { get; set; }
}

public sealed class UpdateBranchAdminRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? BranchType { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public int? MaxTables { get; set; }
    public int? MaxCapacity { get; set; }
    public bool? IsActive { get; set; }
}
