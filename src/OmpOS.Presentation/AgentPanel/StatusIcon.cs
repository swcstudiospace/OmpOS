namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// The keys of the status glyphs in the Agent Panel glyph table (46-UI-SPEC). A view maps each key to its path data and stroke token;
/// the models only name the glyph, so they stay free of any drawing type. A glyph is decorative: the adjacent status text carries the meaning.
/// </summary>
public static class StatusIcon
{
    /// <summary>Ready or idle: a ring.</summary>
    public const string Ring = "ring";

    /// <summary>Queued or pending: a clock.</summary>
    public const string Clock = "clock";

    /// <summary>Running: a three-quarter ring.</summary>
    public const string Running = "running";

    /// <summary>Needs you, or degraded: an alert triangle.</summary>
    public const string Alert = "alert";

    /// <summary>Awaiting input: a speech bubble.</summary>
    public const string Bubble = "bubble";

    /// <summary>Paused: two bars.</summary>
    public const string Paused = "paused";

    /// <summary>Completed or succeeded: a check in a ring.</summary>
    public const string Check = "check";

    /// <summary>Failed: a cross in a ring.</summary>
    public const string Cross = "cross";

    /// <summary>Lost or unreachable: a question mark in a ring.</summary>
    public const string Question = "question";

    /// <summary>Closed or skipped: a ring with a bar.</summary>
    public const string Closed = "closed";

    /// <summary>Unavailable: a ring with a slash.</summary>
    public const string Slash = "slash";

    /// <summary>Information, and the glyph of an unknown value: a ring with an i.</summary>
    public const string Info = "info";
}
