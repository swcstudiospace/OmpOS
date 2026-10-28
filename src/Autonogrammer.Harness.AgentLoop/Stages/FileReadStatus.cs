namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>The outcome class of a bounded read.</summary>
internal enum FileReadStatus
{
    /// <summary>The file was read.</summary>
    Ok,

    /// <summary>The file does not exist.</summary>
    Missing,

    /// <summary>The file is larger than the cap and nothing was read.</summary>
    TooLarge,

    /// <summary>The file exists but could not be read.</summary>
    Unreadable,
}
