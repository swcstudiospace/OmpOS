using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>
/// The default gate. A stored grant satisfies ByPolicy only, never an every-call rule, and never a destructive call.
/// Auto uses the Manual rules for provider calls. A tainted session asks for every actuating or destructive call.
/// ByProviderGate returns Allow and never Ask. A provider that reports unavailable is refused by the host before this gate runs.
/// </summary>
public sealed class CoworkPolicyGate : ICoworkPolicyGate
{
    private readonly ICoworkGrantStore _grants;
    private readonly Func<CoworkPermissionMode> _mode;

    /// <summary>
    /// Creates the gate.
    /// </summary>
    /// <param name="grants">The grant store.</param>
    /// <param name="mode">Reads the current permission mode. Auto is treated as Manual.</param>
    public CoworkPolicyGate(ICoworkGrantStore grants, Func<CoworkPermissionMode> mode)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(mode);
        _grants = grants;
        _mode = mode;
    }

    /// <inheritdoc />
    public async ValueTask<CoworkPolicyDecision> DecideAsync(CoworkPolicyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Tool.Approval == CoworkApprovalRequirement.ByProviderGate)
        {
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Allow, false, null);
        }

        if (request.Tool.Approval == CoworkApprovalRequirement.EveryCallOnExecutingDevice)
        {
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Ask, true, null);
        }

        if (request.Tool.Approval == CoworkApprovalRequirement.EveryCall)
        {
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Ask, false, null);
        }

        var taintedActuating = request.SessionTainted
            && request.Tool.Effect is CoworkToolEffect.Actuating or CoworkToolEffect.Destructive;
        if (taintedActuating)
        {
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Ask, false, null);
        }

        if (_mode() == CoworkPermissionMode.Skip)
        {
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Allow, false, null);
        }

        if (request.Tool.Effect != CoworkToolEffect.Destructive
            && request.Preview.Grant is { } grant
            && await _grants.IsGrantedAsync(grant, cancellationToken).ConfigureAwait(false))
        {
            return new CoworkPolicyDecision(CoworkPolicyOutcome.Allow, false, null);
        }

        return new CoworkPolicyDecision(CoworkPolicyOutcome.Ask, false, null);
    }
}
