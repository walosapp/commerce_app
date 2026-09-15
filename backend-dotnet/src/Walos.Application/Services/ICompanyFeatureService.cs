using Walos.Application.DTOs.Features;

namespace Walos.Application.Services;

public interface ICompanyFeatureService
{
    Task<IReadOnlyList<FeatureDefinitionResponse>> GetFeatureCatalogAsync();
    Task<IReadOnlyList<CompanyFeatureResponse>> GetCompanyFeaturesAsync(long companyId);
    Task<bool> IsFeatureEnabledAsync(long companyId, string featureCode);
    Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(
        long companyId,
        IReadOnlyCollection<string> featureCodes);
    Task SetCompanyFeatureAsync(long companyId, string featureCode, bool isEnabled, long updatedBy);
}
