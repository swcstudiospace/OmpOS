namespace Autonogrammer.Presentation.Commands;

/// <summary>How a native action ended.</summary>
public enum SlashActionOutcome
{
    /// <summary>The action ran.</summary>
    Completed = 0,

    /// <summary>The action started and failed; the message says why.</summary>
    Failed = 1,

    /// <summary>The head cannot run the action now; the message says why.</summary>
    NotAvailable = 2,
}
