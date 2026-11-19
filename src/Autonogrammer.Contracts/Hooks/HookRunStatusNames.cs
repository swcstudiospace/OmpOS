namespace Autonogrammer.Contracts.Hooks;

/// <summary>
/// The wire strings of <see cref="HookRunEvent.Status"/>. Every producer and every log spells a status through these constants, so a status is
/// renamed in one place.
/// </summary>
public static class HookRunStatusNames
{
    /// <summary>The hook ran and its answer was used.</summary>
    public const string Succeeded = "succeeded";

    /// <summary>The hook denied the action.</summary>
    public const string Denied = "denied";

    /// <summary>The hook crashed, exited with an error or could not start.</summary>
    public const string Failed = "failed";

    /// <summary>The hook outlived its timeout and was stopped.</summary>
    public const string TimedOut = "timedOut";

    /// <summary>The hook did not start.</summary>
    public const string Skipped = "skipped";

    /// <summary>Autonogrammer itself failed to start the hook, so its source was quarantined.</summary>
    public const string Faulted = "faulted";

    /// <summary>The hook did not start because you have not consented to it.</summary>
    public const string AwaitingConsent = "awaitingConsent";

    /// <summary>The hook did not start because its source is quarantined.</summary>
    public const string PluginQuarantined = "pluginQuarantined";

    /// <summary>The hook answered with something Autonogrammer could not use.</summary>
    public const string InvalidResponse = "invalidResponse";
}
