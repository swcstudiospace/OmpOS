namespace OmpOS.Contracts.Intents;

/// <summary>
/// Recognizer outcomes of the catch-all prompt parser. Every input yields
/// exactly one envelope; <see cref="Fallback"/> is the unremovable terminator.
/// </summary>
public enum IntentKind
{
    /// <summary>Plain chat text with no recognized structure.</summary>
    Chat,

    /// <summary>Slash command (for example "/resume").</summary>
    SlashCommand,

    /// <summary>At-mention or file reference.</summary>
    Mention,

    /// <summary>Structured directive.</summary>
    Directive,

    /// <summary>omp magic keyword.</summary>
    Magic,

    /// <summary>Mid-run steering of an active prompt.</summary>
    Steer,

    /// <summary>Follow-up on a completed prompt.</summary>
    FollowUp,

    /// <summary>Unremovable fallback; matches when nothing else does.</summary>
    Fallback,
}
