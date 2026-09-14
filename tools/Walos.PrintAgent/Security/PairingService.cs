using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Walos.PrintAgent.Api;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Security;

public sealed class PairingService
{
    public static readonly TimeSpan DefaultPairingCodeLifetime = TimeSpan.FromMinutes(5);

    private readonly AgentStateStore _store;
    private readonly ITokenProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pairingCodeLifetime;
    private readonly SemaphoreSlim _pairingGate = new(1, 1);
    private readonly object _challengeGate = new();
    private string _pairingCode;
    private DateTimeOffset _pairingCodeExpiresAtUtc;

    [ActivatorUtilitiesConstructor]
    public PairingService(AgentStateStore store, ITokenProtector protector)
        : this(store, protector, TimeProvider.System, DefaultPairingCodeLifetime)
    {
    }

    public PairingService(
        AgentStateStore store,
        ITokenProtector protector,
        TimeProvider timeProvider,
        TimeSpan pairingCodeLifetime)
    {
        _store = store;
        _protector = protector;
        _timeProvider = timeProvider;
        _pairingCodeLifetime = pairingCodeLifetime > TimeSpan.Zero
            ? pairingCodeLifetime
            : throw new ArgumentOutOfRangeException(nameof(pairingCodeLifetime));
        _pairingCode = CreatePairingCode();
        _pairingCodeExpiresAtUtc = _timeProvider.GetUtcNow().Add(_pairingCodeLifetime);
    }

    public string CurrentPairingCode
    {
        get
        {
            lock (_challengeGate)
            {
                RotateIfExpiredUnsafe();
                return _pairingCode;
            }
        }
    }

    public async Task<PairResponse?> PairAsync(PairRequest request, CancellationToken ct)
    {
        await _pairingGate.WaitAsync(ct);
        try
        {
            lock (_challengeGate)
            {
                if (_timeProvider.GetUtcNow() >= _pairingCodeExpiresAtUtc ||
                    !FixedTimeEquals(request.PairingCode, _pairingCode))
                {
                    RotateIfExpiredUnsafe();
                    return null;
                }
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var protectedToken = _protector.Protect(token);
            await _store.SetPairingAsync(
                protectedToken,
                new WorkstationIdentity(request.CompanyId, request.BranchId, request.WorkstationId),
                ct);

            lock (_challengeGate)
            {
                RotateUnsafe();
            }
            return new PairResponse(token, "Bearer", _store.AgentId);
        }
        finally
        {
            _pairingGate.Release();
        }
    }

    public bool ValidateToken(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var protectedToken = _store.GetProtectedToken();
        if (protectedToken is null)
        {
            return false;
        }

        try
        {
            return FixedTimeEquals(candidate, _protector.Unprotect(protectedToken));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string CreatePairingCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private void RotateIfExpiredUnsafe()
    {
        if (_timeProvider.GetUtcNow() >= _pairingCodeExpiresAtUtc)
        {
            RotateUnsafe();
        }
    }

    private void RotateUnsafe()
    {
        _pairingCode = CreatePairingCode();
        _pairingCodeExpiresAtUtc = _timeProvider.GetUtcNow().Add(_pairingCodeLifetime);
    }
}
