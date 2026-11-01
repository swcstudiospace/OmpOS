namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Decides allow, ask or deny before a provider runs.</summary>
public interface ICoworkPolicyGate
{
    /// <summary>Decides the call.</summary>
    /// <param name="request">The call, the tool and the preview.</param>
    /// <param name="cancellationToken">Cancels the decision.</param>
    /// <returns>The decision.</returns>
    ValueTask<CoworkPolicyDecision> DecideAsync(CoworkPolicyRequest request, CancellationToken cancellationToken);
}
