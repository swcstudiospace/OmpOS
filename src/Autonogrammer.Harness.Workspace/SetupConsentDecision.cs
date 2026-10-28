namespace Autonogrammer.Workspace;

/// <summary>
/// The user's answer to a setup consent prompt.
/// </summary>
public sealed record SetupConsentDecision
{
    private SetupConsentDecision(bool isGranted, string? reason)
    {
        IsGranted = isGranted;
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the user approved running the setup steps.
    /// </summary>
    public bool IsGranted { get; }

    /// <summary>
    /// Gets why the setup was not approved, or null when it was approved.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Creates an approval.
    /// </summary>
    public static SetupConsentDecision Granted() => new(true, null);

    /// <summary>
    /// Creates a refusal.
    /// </summary>
    /// <param name="reason">Why the setup was not approved. It is shown to the user.</param>
    public static SetupConsentDecision Denied(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new SetupConsentDecision(false, reason);
    }
}
