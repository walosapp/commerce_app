using Walos.Application.DTOs.Features;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class CompanyFeatureService : ICompanyFeatureService
{
    private readonly ICompanyFeatureRepository _repository;

    public CompanyFeatureService(ICompanyFeatureRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<FeatureDefinitionResponse>> GetFeatureCatalogAsync()
    {
        var features = await _repository.GetFeatureCatalogAsync();
        return features
            .Select(feature => new FeatureDefinitionResponse(
                feature.Code,
                feature.Name,
                feature.Description,
                feature.DefaultEnabled,
                feature.IsMandatory,
                feature.IsActive,
                feature.DisplayOrder))
            .ToList();
    }

    public async Task<IReadOnlyList<CompanyFeatureResponse>> GetCompanyFeaturesAsync(long companyId)
    {
        if (!await _repository.CompanyExistsAsync(companyId))
            throw new NotFoundException("Comercio no encontrado");

        var features = await _repository.GetCompanyFeaturesAsync(companyId);
        return features
            .Select(feature => new CompanyFeatureResponse(
                feature.FeatureCode,
                feature.Name,
                feature.Description,
                feature.IsEnabled,
                feature.IsMandatory,
                feature.DisplayOrder,
                feature.UpdatedAt,
                feature.UpdatedBy))
            .ToList();
    }

    public Task<bool> IsFeatureEnabledAsync(long companyId, string featureCode)
    {
        var normalizedCode = WalosFeatures.Normalize(featureCode);
        return WalosFeatures.IsKnown(normalizedCode)
            ? _repository.IsFeatureEnabledAsync(companyId, normalizedCode)
            : Task.FromResult(false);
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(
        long companyId,
        IReadOnlyCollection<string> featureCodes)
    {
        var normalized = featureCodes
            .Select(WalosFeatures.Normalize)
            .Where(WalosFeatures.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
            return new Dictionary<string, bool>(StringComparer.Ordinal);

        return await _repository.GetFeatureStatesAsync(companyId, normalized);
    }

    public async Task SetCompanyFeatureAsync(
        long companyId,
        string featureCode,
        bool isEnabled,
        long updatedBy)
    {
        var normalizedCode = WalosFeatures.Normalize(featureCode);
        if (!WalosFeatures.IsKnown(normalizedCode))
            throw new ValidationException("Funcionalidad desconocida");

        if (normalizedCode == WalosFeatures.Dashboard && !isEnabled)
            throw new ValidationException("El dashboard es una funcionalidad obligatoria");

        if (!await _repository.CompanyExistsAsync(companyId))
            throw new NotFoundException("Comercio no encontrado");

        await _repository.SetCompanyFeatureAsync(companyId, normalizedCode, isEnabled, updatedBy);
    }
}
