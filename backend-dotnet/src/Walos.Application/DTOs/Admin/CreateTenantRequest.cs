namespace Walos.Application.DTOs.Admin;

public class CreateTenantRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Country { get; set; } = "CO";
    public string? PostalCode { get; set; }
    public string Currency { get; set; } = "COP";
    public string Timezone { get; set; } = "America/Bogota";
    public string Language { get; set; } = "es";

    public string BranchName { get; set; } = string.Empty;
    public string BranchType { get; set; } = "bar";

    public string AdminFirstName { get; set; } = string.Empty;
    public string AdminLastName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}
