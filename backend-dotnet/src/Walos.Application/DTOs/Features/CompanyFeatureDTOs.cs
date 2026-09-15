namespace Walos.Application.DTOs.Features;

public sealed record FeatureDefinitionResponse(
    string Code,
    string Name,
    string? Description,
    bool DefaultEnabled,
    bool IsMandatory,
    bool IsActive,
    int DisplayOrder);

public sealed record CompanyFeatureResponse(
    string Code,
    string Name,
    string? Description,
    bool IsEnabled,
    bool IsMandatory,
    int DisplayOrder,
    DateTime? UpdatedAt,
    long? UpdatedBy);

public sealed record UpdateCompanyFeatureRequest(bool? IsEnabled);
