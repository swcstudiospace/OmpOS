namespace Autonogrammer.UI.Components.Terminal;

/// <summary>What <see cref="TerminalChunkSequencer.Accept"/> decided about one chunk.</summary>
public enum TerminalChunkOutcome
{
    /// <summary>The chunk is the next one in order; its text is to be written.</summary>
    Accepted,

    /// <summary>The chunk was already written (at-least-once delivery); it is dropped.</summary>
    Duplicate,

    /// <summary>A chunk is missing before this one; nothing is written and the stream must be attached again.</summary>
    Gap,
}
