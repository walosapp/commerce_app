using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Features;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/features")]
[Authorize]
public sealed class FeaturesController : ControllerBase
{
    private readonly ICompanyFeatureService _features;
    private readonly ITenantContext _tenant;

    public FeaturesController(ICompanyFeatureService features, ITenantContext tenant)
    {
        _features = features;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrentCompanyFeatures()
    {
        var result = await _features.GetCompanyFeaturesAsync(_tenant.CompanyId);
        return Ok(ApiResponse<IReadOnlyList<CompanyFeatureResponse>>.Ok(result));
    }
}
