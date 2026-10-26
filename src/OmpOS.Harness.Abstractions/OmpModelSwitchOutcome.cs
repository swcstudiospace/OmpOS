namespace OmpOS.Harness.Abstractions;

/// <summary>How an attempt to switch the model of a session ended.</summary>
public enum OmpModelSwitchOutcome
{
    /// <summary>The engine confirmed the model: it is now the current model.</summary>
    Switched,

    /// <summary>The session is not open, or its connection is gone. Nothing was sent.</summary>
    NotOpen,

    /// <summary>A prompt or another switch is running. Nothing was sent.</summary>
    Busy,

    /// <summary>The engine offers no model option for the session. Nothing was sent.</summary>
    NoModelOption,

    /// <summary>The requested value is not among the models the engine offers. Nothing was sent.</summary>
    UnknownModel,

    /// <summary>The request was sent and the engine refused it, did not answer, or did not confirm the model.</summary>
    Failed,
}
