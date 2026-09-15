using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Features;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/platform/admin")]
[Authorize(Policy = WalosPolicies.PlatformAdmin)]
public sealed class PlatformFeaturesController : ControllerBase
{
    private readonly ICompanyFeatureService _features;
    private readonly ITenantContext _tenant;

    public PlatformFeaturesController(ICompanyFeatureService features, ITenantContext tenant)
    {
        _features = features;
        _tenant = tenant;
    }

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatureCatalog()
    {
        var result = await _features.GetFeatureCatalogAsync();
        return Ok(ApiResponse<IReadOnlyList<FeatureDefinitionResponse>>.Ok(result));
    }

    [HttpGet("companies/{companyId:long}/features")]
    public async Task<IActionResult> GetCompanyFeatures(long companyId)
    {
        var result = await _features.GetCompanyFeaturesAsync(companyId);
        return Ok(ApiResponse<IReadOnlyList<CompanyFeatureResponse>>.Ok(result));
    }

    [HttpPut("companies/{companyId:long}/features/{featureCode}")]
    public async Task<IActionResult> SetCompanyFeature(
        long companyId,
        string featureCode,
        [FromBody] UpdateCompanyFeatureRequest request)
    {
        if (!request.IsEnabled.HasValue)
        {
            return BadRequest(ApiResponse.Fail(
                "El campo isEnabled es obligatorio",
                "validation_error"));
        }

        if (!request.IsEnabled.Value
            && WalosFeatures.Normalize(featureCode) == WalosFeatures.Dashboard)
        {
            return BadRequest(ApiResponse.Fail(
                "El dashboard es una funcionalidad obligatoria",
                "feature_always_enabled"));
        }

        await _features.SetCompanyFeatureAsync(
            companyId, featureCode, request.IsEnabled.Value, _tenant.UserId);
        return Ok(ApiResponse.Ok("Módulo actualizado"));
    }
}
