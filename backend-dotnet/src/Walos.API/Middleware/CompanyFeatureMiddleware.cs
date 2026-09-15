using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.Services;
using Walos.Domain.Interfaces;

namespace Walos.API.Middleware;

public sealed class CompanyFeatureMiddleware
{
    private readonly RequestDelegate _next;

    public CompanyFeatureMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenant,
        ICompanyFeatureService features)
    {
        var required = context.GetEndpoint()?.Metadata.GetOrderedMetadata<RequireFeatureAttribute>()
            ?? Array.Empty<RequireFeatureAttribute>();
        var anyRequired = context.GetEndpoint()?.Metadata.GetOrderedMetadata<RequireAnyFeatureAttribute>()
            ?? Array.Empty<RequireAnyFeatureAttribute>();

        if (required.Count == 0 && anyRequired.Count == 0)
        {
            await _next(context);
            return;
        }

        // The bypass is intentionally narrower than a role check: only the trusted
        // system dev principal materialized from the signed JWT may bypass tenant flags.
        if (tenant.IsDev && tenant.IsPlatformAdmin)
        {
            await _next(context);
            return;
        }

        var requestedCodes = required.Select(requirement => requirement.Feature)
            .Concat(anyRequired.SelectMany(requirement => requirement.Features))
            .Select(Walos.Domain.Features.WalosFeatures.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var states = await features.GetFeatureStatesAsync(tenant.CompanyId, requestedCodes);

        foreach (var requirement in required)
        {
            var code = Walos.Domain.Features.WalosFeatures.Normalize(requirement.Feature);
            if (!states.TryGetValue(code, out var enabled) || !enabled)
            {
                await WriteDeniedAsync(context, requirement.Feature);
                return;
            }
        }

        foreach (var requirement in anyRequired)
        {
            var enabled = requirement.Features.Any(feature =>
                states.TryGetValue(Walos.Domain.Features.WalosFeatures.Normalize(feature), out var state)
                && state);

            if (!enabled)
            {
                await WriteDeniedAsync(context, string.Join('|', requirement.Features));
                return;
            }
        }

        await _next(context);
    }

    private static async Task WriteDeniedAsync(HttpContext context, string feature)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "El módulo no está habilitado para este comercio",
            "feature_not_enabled",
            new { feature }));
    }
}
