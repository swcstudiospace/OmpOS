namespace Autonogrammer.Presentation.Editor;

/// <summary>The decision on one diff hunk.</summary>
public enum HunkDecision
{
    /// <summary>No decision yet. The hunk is not applied, so the original text stays.</summary>
    Pending,

    /// <summary>The hunk is kept: its new lines replace its original lines.</summary>
    Accept,

    /// <summary>The hunk is reverted: the original lines stay.</summary>
    Reject,
}
