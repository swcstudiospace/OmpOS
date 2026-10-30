namespace Autonogrammer.Presentation.Remote;

/// <summary>How a <see cref="RemoteEnvironmentStatus"/> is drawn: the words always say the state, the severity only picks the emphasis.</summary>
public enum RemoteStatusSeverity
{
    /// <summary>Nothing happened yet or the session ended normally.</summary>
    Neutral,

    /// <summary>Something is in progress.</summary>
    Progress,

    /// <summary>It works.</summary>
    Good,

    /// <summary>It works with a caveat, or it is recovering.</summary>
    Warning,

    /// <summary>It does not work until the user acts.</summary>
    Error,
}
