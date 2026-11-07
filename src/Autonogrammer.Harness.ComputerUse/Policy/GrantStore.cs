using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>
/// Thread-safe in-memory grants. Only <see cref="TryCreateFromUser"/> constructs a grant.
/// Dispatch grants end at the earlier of command end and 30 minutes, never auto-approve typing, and withdraw on device revoke.
/// </summary>
public sealed class GrantStore : IComputerGrantRegistry
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly List<ComputerGrant> _grants = [];
    private readonly Dictionary<string, string> _windowSets = new(StringComparer.Ordinal);
    private int _sequence;

    /// <summary>Creates a store.</summary>
    /// <param name="time">Clock. Tests pass a manual provider.</param>
    public GrantStore(TimeProvider time)
    {
        _timeProvider = time;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ComputerGrantView> Snapshot()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            return _grants.Where(grant => grant.Expiry > now).Select(static grant => grant.ToView()).ToArray();
        }
    }

    /// <inheritdoc/>
    public void Revoke(string grantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(grantId);
        lock (_gate)
        {
            _grants.RemoveAll(grant => string.Equals(grant.GrantId, grantId, StringComparison.Ordinal));
        }
    }

    /// <summary>User-grant path. The model cannot call this.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="appIdentity">Application identity.</param>
    /// <param name="tier">Tier.</param>
    /// <param name="origin">Origin.</param>
    /// <param name="commandEnd">Command end for Dispatch, or the requested local end.</param>
    /// <param name="autoApproveTyping">Requested flag. Forced off for Dispatch.</param>
    /// <param name="grant">The created grant.</param>
    /// <returns><see langword="true"/> when the grant was stored.</returns>
    public bool TryCreateFromUser(
        string sessionId,
        string appIdentity,
        ComputerGrantTier tier,
        ComputerActionOrigin origin,
        DateTimeOffset? commandEnd,
        bool autoApproveTyping,
        out ComputerGrant grant)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(appIdentity);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset expiry = origin == ComputerActionOrigin.Dispatch
            ? DispatchExpiry(now, commandEnd)
            : LocalExpiry(now, commandEnd);
        bool typing = origin == ComputerActionOrigin.Dispatch ? false : autoApproveTyping;
        grant = new ComputerGrant
        {
            GrantId = NextId(),
            SessionId = sessionId,
            AppIdentity = appIdentity,
            Tier = tier,
            Origin = origin,
            Expiry = expiry,
            AutoApproveTyping = typing,
        };
        lock (_gate)
        {
            _grants.Add(grant);
        }

        return true;
    }

    /// <summary>Model-created grants are refused. This method never stores a grant.</summary>
    /// <returns><see langword="false"/>.</returns>
    public bool TryCreateFromModel() => _sequence < 0;

    /// <summary>Ends every grant for the session.</summary>
    /// <param name="sessionId">Session id.</param>
    public void EndSession(string sessionId)
    {
        lock (_gate)
        {
            _grants.RemoveAll(grant => string.Equals(grant.SessionId, sessionId, StringComparison.Ordinal));
            _windowSets.Remove(sessionId);
        }
    }

    /// <summary>Ends Dispatch grants for the session when the command ends.</summary>
    /// <param name="sessionId">Session id.</param>
    public void EndCommand(string sessionId)
    {
        lock (_gate)
        {
            _grants.RemoveAll(grant =>
                string.Equals(grant.SessionId, sessionId, StringComparison.Ordinal)
                && grant.Origin == ComputerActionOrigin.Dispatch);
        }
    }

    /// <summary>Withdraws every Dispatch-origin grant. Local grants stay.</summary>
    public void RevokeDevice()
    {
        lock (_gate)
        {
            _grants.RemoveAll(grant => grant.Origin == ComputerActionOrigin.Dispatch);
        }
    }

    /// <summary>Ends the session's grants when the window-set identity changes.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="identities">Current application identities.</param>
    public void ObserveWindowIdentities(string sessionId, IReadOnlyCollection<string> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        string fingerprint = string.Join('\u001f', identities.Order(StringComparer.Ordinal));
        lock (_gate)
        {
            if (_windowSets.TryGetValue(sessionId, out string? previous) && !string.Equals(previous, fingerprint, StringComparison.Ordinal))
            {
                _grants.RemoveAll(grant => string.Equals(grant.SessionId, sessionId, StringComparison.Ordinal));
            }

            _windowSets[sessionId] = fingerprint;
        }
    }

    /// <summary>Active grant for the session, application and origin.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="appIdentity">Application identity.</param>
    /// <param name="origin">Origin.</param>
    /// <returns>The grant, or <see langword="null"/>.</returns>
    public ComputerGrant? FindActive(string sessionId, string appIdentity, ComputerActionOrigin origin)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            for (int i = _grants.Count - 1; i >= 0; i--)
            {
                ComputerGrant grant = _grants[i];
                if (grant.Expiry > now
                    && string.Equals(grant.SessionId, sessionId, StringComparison.Ordinal)
                    && string.Equals(grant.AppIdentity, appIdentity, StringComparison.OrdinalIgnoreCase)
                    && grant.Origin == origin)
                {
                    return grant;
                }
            }
        }

        return null;
    }

    private string NextId()
    {
        int value = Interlocked.Increment(ref _sequence);
        return "grant-" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset LocalExpiry(DateTimeOffset now, DateTimeOffset? requested)
    {
        DateTimeOffset cap = now + ComputerGrantPolicy.LocalMaximum;
        if (requested is null || requested.Value > cap)
        {
            return cap;
        }

        return requested.Value;
    }

    private static DateTimeOffset DispatchExpiry(DateTimeOffset now, DateTimeOffset? commandEnd)
    {
        DateTimeOffset cap = now + ComputerGrantPolicy.DispatchMaximum;
        if (commandEnd is null || commandEnd.Value > cap)
        {
            return cap;
        }

        return commandEnd.Value;
    }
}
