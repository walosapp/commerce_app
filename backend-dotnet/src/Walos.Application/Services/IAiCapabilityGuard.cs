namespace Walos.Application.Services;

public interface IAiCapabilityGuard
{
    Task<AiCapabilitySnapshot> GetSnapshotAsync(long companyId, bool trustedDevBypass = false);
}

public sealed class AiCapabilitySnapshot
{
    private readonly IReadOnlyDictionary<string, bool> _states;

    public AiCapabilitySnapshot(IReadOnlyDictionary<string, bool> states, string fingerprint)
    {
        _states = states;
        Fingerprint = fingerprint;
    }

    public string Fingerprint { get; }

    public bool IsEnabled(string feature) =>
        _states.TryGetValue(feature, out var enabled) && enabled;

    public void Ensure(params string[] requiredFeatures)
    {
        foreach (var feature in requiredFeatures)
        {
            if (!IsEnabled(feature))
                throw new Walos.Domain.Exceptions.FeatureNotEnabledException(feature);
        }
    }
}
