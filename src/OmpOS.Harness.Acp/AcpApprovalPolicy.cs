using System.Collections.Frozen;

namespace OmpOS.Harness.Acp;

/// <summary>
/// Decides whether an agent permission request is granted. Deny is the default: a request is granted only when its
/// tool kind is in the configured allowlist. The optional narrowing hook may refuse a request that the allowlist
/// admits, but it is never consulted for a kind outside the allowlist, so it can never grant more than the allowlist.
/// </summary>
public sealed class AcpApprovalPolicy
{
    private readonly FrozenSet<string> _allowedKinds;
    private readonly Func<AcpPermissionRequest, bool>? _narrow;

    /// <summary>Initializes a new instance of the <see cref="AcpApprovalPolicy"/> class.</summary>
    /// <param name="allowedKinds">The tool kinds that may be granted. Empty denies everything.</param>
    /// <param name="narrow">An optional hook that may refuse an allowlisted request. Its result cannot grant a request.</param>
    public AcpApprovalPolicy(IEnumerable<string> allowedKinds, Func<AcpPermissionRequest, bool>? narrow = null)
    {
        ArgumentNullException.ThrowIfNull(allowedKinds);
        var kinds = allowedKinds.ToArray();
        if (Array.Exists(kinds, static kind => kind is null))
        {
            throw new ArgumentException("Allowed kinds must not contain null.", nameof(allowedKinds));
        }

        _allowedKinds = kinds.ToFrozenSet(StringComparer.Ordinal);
        _narrow = narrow;
    }

    /// <summary>Gets a policy that denies every permission request.</summary>
    public static AcpApprovalPolicy DenyAll { get; } = new([]);

    /// <summary>Returns whether the request is granted.</summary>
    /// <param name="request">The permission request.</param>
    /// <returns><see langword="true"/> only when the kind is allowlisted and the narrowing hook (if any) accepts it.</returns>
    public bool Allows(AcpPermissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind.Length == 0 || !_allowedKinds.Contains(request.Kind))
        {
            return false;
        }

        return _narrow?.Invoke(request) ?? true;
    }
}
