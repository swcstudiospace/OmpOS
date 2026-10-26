namespace OmpOS.Harness.Abstractions;

/// <summary>How an attempt to change an option of a session ended.</summary>
public enum OmpOptionSetOutcome
{
    /// <summary>The engine confirmed the value: it is now the current value of the option.</summary>
    Set,

    /// <summary>The session is not open, or its connection is gone. Nothing was sent.</summary>
    NotOpen,

    /// <summary>A prompt or another change is running. Nothing was sent.</summary>
    Busy,

    /// <summary>The engine offers no option with that identifier. Nothing was sent.</summary>
    UnknownOption,

    /// <summary>The requested value is not among the values the engine offers for the option. Nothing was sent.</summary>
    UnknownValue,

    /// <summary>The request was sent and the engine refused it, did not answer, or did not confirm the value.</summary>
    Failed,
}
