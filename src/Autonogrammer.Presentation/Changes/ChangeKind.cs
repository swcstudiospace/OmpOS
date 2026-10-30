namespace Autonogrammer.Presentation.Changes;

/// <summary>What the agent's edits did to a file, as far as the transcript tells.</summary>
public enum ChangeKind
{
    /// <summary>The agent changed a file that existed.</summary>
    Edited,

    /// <summary>The agent created the file: its first edit had no text before it.</summary>
    Created,

    /// <summary>The agent left the file empty. An agent that deletes a file reports the same edit, so the transcript cannot tell the two apart.</summary>
    Emptied,
}
