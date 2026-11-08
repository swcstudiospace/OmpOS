namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>The result of reading one probe answer: a state, or the reason the answer could not be read.</summary>
/// <param name="State">The state when the answer parsed; otherwise null.</param>
/// <param name="Failure">The reason the answer did not parse; otherwise null.</param>
internal sealed record RemoteProbeOutcome(RemoteState? State, string? Failure)
{
    /// <summary>Returns the outcome of an answer that parsed.</summary>
    /// <param name="state">The state the answer describes.</param>
    /// <returns>The outcome.</returns>
    public static RemoteProbeOutcome Parsed(RemoteState state) => new(state, null);

    /// <summary>Returns the outcome of an answer that did not parse.</summary>
    /// <param name="failure">What was wrong with it.</param>
    /// <returns>The outcome.</returns>
    public static RemoteProbeOutcome Unreadable(string failure) => new(null, failure);
}
