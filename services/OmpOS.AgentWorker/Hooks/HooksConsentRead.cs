namespace OmpOS.AgentWorker.Hooks;

/// <summary>The outcome of reading the consent snapshot from a connection's token: a snapshot, a refusal reason, or neither when the token carries none.</summary>
/// <param name="Snapshot">The snapshot, or null.</param>
/// <param name="Problem">Why a claim that is present was refused, or null. The text is fixed, never copied from the token.</param>
internal sealed record HooksConsentRead(HooksConsentSnapshot? Snapshot, string? Problem)
{
    /// <summary>Gets the outcome for a token without a snapshot claim.</summary>
    public static HooksConsentRead Absent { get; } = new(null, null);
}
