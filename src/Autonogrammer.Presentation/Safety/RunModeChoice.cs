namespace Autonogrammer.Presentation.Safety;

/// <summary>The run mode as the Run mode page shows it. It mirrors the engine's run mode, which the presentation layer cannot reference.</summary>
public enum RunModeChoice
{
    /// <summary>Allowlist, then read-only known-safe commands, then ask. There is no classifier.</summary>
    AutoReview = 0,

    /// <summary>Allowlist, then ask.</summary>
    Allowlist = 1,

    /// <summary>Run every call without asking.</summary>
    RunEverything = 2,
}
