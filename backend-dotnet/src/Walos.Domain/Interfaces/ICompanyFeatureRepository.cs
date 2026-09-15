using Walos.Domain.Entities.Platform;

namespace Walos.Domain.Interfaces;

public interface ICompanyFeatureRepository
{
    Task<bool> CompanyExistsAsync(long companyId);
    Task<IReadOnlyList<FeatureDefinition>> GetFeatureCatalogAsync();
    Task<IReadOnlyList<CompanyFeature>> GetCompanyFeaturesAsync(long companyId);
    Task<bool> IsFeatureEnabledAsync(long companyId, string featureCode);
    Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(
        long companyId,
        IReadOnlyCollection<string> featureCodes);
    Task SetCompanyFeatureAsync(long companyId, string featureCode, bool isEnabled, long updatedBy);
}
