using System.Security.Cryptography;
using System.Text;
using Walos.Domain.Features;

namespace Walos.Application.Services;

public sealed class AiCapabilityGuard : IAiCapabilityGuard
{
    private static readonly string[] CapabilityCodes =
    [
        WalosFeatures.Ai,
        WalosFeatures.Inventory,
        WalosFeatures.Purchases,
        WalosFeatures.Suppliers,
        WalosFeatures.Delivery
    ];

    private readonly ICompanyFeatureService _features;

    public AiCapabilityGuard(ICompanyFeatureService features) => _features = features;

    public async Task<AiCapabilitySnapshot> GetSnapshotAsync(long companyId, bool trustedDevBypass = false)
    {
        IReadOnlyDictionary<string, bool> states;
        if (trustedDevBypass)
        {
            states = CapabilityCodes.ToDictionary(code => code, _ => true, StringComparer.Ordinal);
        }
        else
        {
            var stored = await _features.GetFeatureStatesAsync(companyId, CapabilityCodes);
            states = CapabilityCodes.ToDictionary(
                code => code,
                code => stored.TryGetValue(code, out var enabled) && enabled,
                StringComparer.Ordinal);
        }

        var canonical = string.Join('|', CapabilityCodes
            .OrderBy(code => code, StringComparer.Ordinal)
            .Select(code => $"{code}={(states[code] ? '1' : '0')}"));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

        return new AiCapabilitySnapshot(states, fingerprint);
    }
}
