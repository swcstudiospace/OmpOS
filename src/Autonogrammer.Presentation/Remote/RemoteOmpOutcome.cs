namespace Autonogrammer.Presentation.Remote;

/// <summary>What an Omp check or install of one remote environment reported. The page says each outcome in words.</summary>
public enum RemoteOmpOutcome
{
    /// <summary>The machine's omp is current. No install is offered.</summary>
    UpToDate,

    /// <summary>A check found lines an install would apply. Install Omp applies exactly these lines.</summary>
    Planned,

    /// <summary>The install did not run, because the machine was not checked or changed since the check. Nothing was written; the user checks again.</summary>
    NeedsCheck,

    /// <summary>Every checked line was applied. The user checks again to see the new state.</summary>
    Installed,

    /// <summary>An install line failed or was cut short. Lines may have been applied before it; the user checks again.</summary>
    Failed,

    /// <summary>Omp cannot be installed on this environment, for the reason given. Nothing was written.</summary>
    Refused,

    /// <summary>The environment could not be reached or read, for the reason given. Nothing was written.</summary>
    Unavailable,
}
