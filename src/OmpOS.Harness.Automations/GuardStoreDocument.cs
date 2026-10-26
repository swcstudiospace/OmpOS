namespace OmpOS.Harness.Automations;

/// <summary>
/// The on-disk shape of the guard store: a version, the pause of each paused automation and the start instants of the
/// runs admitted for each automation.
/// </summary>
internal sealed class GuardStoreDocument
{
    /// <summary>The document version. Only <see cref="FileAutomationGuardStore.CurrentVersion"/> is read.</summary>
    public int Version { get; set; }

    /// <summary>The pause of each paused automation, keyed by automation identifier.</summary>
    public Dictionary<string, GuardStorePause>? Pauses { get; set; }

    /// <summary>The start instants of the admitted runs, keyed by automation identifier.</summary>
    public Dictionary<string, List<DateTimeOffset>>? Runs { get; set; }
}
