namespace OmpOS.Presentation.Hooks;

/// <summary>What happened to one hook in one run.</summary>
public enum HookRunOutcome
{
    /// <summary>The hook ran and its answer was used.</summary>
    Succeeded = 0,

    /// <summary>The hook denied the action.</summary>
    Denied = 1,

    /// <summary>The hook crashed, exited with an error or could not start.</summary>
    Failed = 2,

    /// <summary>The hook outlived its timeout and was stopped.</summary>
    TimedOut = 3,

    /// <summary>The hook did not start: its matcher is invalid or it may not run here.</summary>
    Skipped = 4,

    /// <summary>OmpOS itself failed to start the hook, so its source was quarantined.</summary>
    Faulted = 5,

    /// <summary>The hook did not start because you have not consented to it.</summary>
    AwaitingConsent = 6,

    /// <summary>The hook did not start because its source is quarantined.</summary>
    Quarantined = 7,

    /// <summary>The hook answered with something OmpOS could not use.</summary>
    InvalidResponse = 8,
}
