namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The confirmed installs ran. The outcomes are in the order of the actions: a succeeded one, the one that failed with its reason, and every
/// action after a failure marked not run, because a later install may depend on an earlier one.
/// </summary>
/// <param name="State">The state the probe read before the writes.</param>
/// <param name="Outcomes">One outcome per action.</param>
public sealed record ProvisionCompleted(RemoteState State, IReadOnlyList<ProvisionActionOutcome> Outcomes) : RemoteProvisionResult
{
    /// <summary>Gets whether every action succeeded.</summary>
    public bool Succeeded => Outcomes.All(static outcome => outcome.Status == ProvisionActionStatus.Succeeded);
}
