namespace Autonogrammer.Cowork.Abstractions;

/// <summary>What the policy gate decided.</summary>
public enum CoworkPolicyOutcome
{
    /// <summary>The call may run without asking.</summary>
    Allow = 0,

    /// <summary>The call must be shown to a person.</summary>
    Ask = 1,

    /// <summary>The call is refused.</summary>
    Deny = 2,
}
