namespace Autonogrammer.Presentation.Commands;

/// <summary>How completely Autonogrammer honours a Cursor slash command.</summary>
public enum SlashCommandStatus
{
    /// <summary>Autonogrammer runs the command as Cursor describes it.</summary>
    Built = 0,

    /// <summary>omp runs the command; Autonogrammer only forwards the text.</summary>
    Delegated = 1,

    /// <summary>Autonogrammer runs part of what Cursor describes, or runs it only under a condition the note states.</summary>
    Partial = 2,

    /// <summary>Autonogrammer does not run the command. <see cref="SlashCommandDescriptor.DisabledReason"/> says why.</summary>
    Unavailable = 3,
}
