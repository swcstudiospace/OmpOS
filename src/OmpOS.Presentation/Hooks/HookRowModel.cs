namespace OmpOS.Presentation.Hooks;

/// <summary>
/// One hook in the list, as a head shows it: its trigger, exact command, enabled and consent state, and a degraded marker when the hook
/// cannot run. The row is immutable; the actions on it are methods of the <see cref="HooksViewModel"/> that owns the list.
/// </summary>
public sealed class HookRowModel
{
    /// <summary>Creates the row for one hook.</summary>
    /// <param name="entry">The hook as the source reports it.</param>
    /// <param name="selected">Whether the consent panel shows this hook.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public HookRowModel(HookEntry entry, bool selected)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Id = entry.Id;
        PluginId = entry.PluginId;
        Arguments = entry.Arguments.ToArray();
        WorkingDirectory = entry.WorkingDirectory;
        IsEnabled = entry.IsEnabled;
        IsConsented = entry.IsConsented;
        IsSelected = selected;

        // The runner checks quarantine before consent, so a quarantined plugin's hook reports the quarantine.
        BlockedBy = entry.Quarantine is not null
            ? HookBlockReason.PluginQuarantined
            : entry.IsConsented ? HookBlockReason.None : HookBlockReason.AwaitingConsent;

        TriggerText = "Trigger: " + entry.Trigger;
        CommandText = "Command: " + HookCommandText.CommandLine(Arguments);
        WorkingDirectoryText = "Working directory: " + WorkingDirectory;
        ArgumentListText = HookCommandText.Numbered(Arguments);
        EnabledText = IsEnabled ? "Enabled" : "Disabled";
        ConsentText = IsConsented ? "Consent recorded for this exact command" : "Consent not recorded for this exact command";
        ReviewText = IsSelected ? "Reviewing" : "Review consent";
        DegradedText = DescribeDegradation(BlockedBy, entry.Quarantine);

        EnableName = "Enable hook " + Id;
        DisableName = "Disable hook " + Id;
        MoveUpName = "Move up hook " + Id;
        MoveDownName = "Move down hook " + Id;
        ReviewName = "Review consent for hook " + Id;
    }

    /// <summary>Gets the hook identifier: the plugin identifier and the hook's position in its manifest.</summary>
    public string Id { get; }

    /// <summary>Gets the plugin that declared the hook.</summary>
    public string PluginId { get; }

    /// <summary>Gets the argument list. The first element is the executable.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Gets the working directory the process starts in.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Gets whether the hook runs.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets whether consent is recorded for the hook's exact command.</summary>
    public bool IsConsented { get; }

    /// <summary>Gets whether the consent panel shows this hook.</summary>
    public bool IsSelected { get; }

    /// <summary>Gets what blocks the hook while it cannot run, or <see cref="HookBlockReason.None"/> when nothing does.</summary>
    public HookBlockReason BlockedBy { get; }

    /// <summary>Gets whether the hook is awaiting consent or belongs to a quarantined plugin. Such a row carries the degraded marker.</summary>
    public bool IsDegraded => BlockedBy != HookBlockReason.None;

    /// <summary>Gets the trigger, labelled, in manifest form such as <c>beforeAction</c>.</summary>
    public string TriggerText { get; }

    /// <summary>Gets the exact command line, labelled.</summary>
    public string CommandText { get; }

    /// <summary>Gets the working directory, labelled.</summary>
    public string WorkingDirectoryText { get; }

    /// <summary>Gets every argument on its own line, each with its index, for the consent panel.</summary>
    public string ArgumentListText { get; }

    /// <summary>Gets the enabled state as text.</summary>
    public string EnabledText { get; }

    /// <summary>Gets the consent state as text.</summary>
    public string ConsentText { get; }

    /// <summary>Gets the explanation of the degraded marker, or an empty string when the hook is not degraded.</summary>
    public string DegradedText { get; }

    /// <summary>Gets the text of the review button: whether this hook is the one the consent panel shows.</summary>
    public string ReviewText { get; }

    /// <summary>Gets the accessible name of the Enable button.</summary>
    public string EnableName { get; }

    /// <summary>Gets the accessible name of the Disable button.</summary>
    public string DisableName { get; }

    /// <summary>Gets the accessible name of the Move up button.</summary>
    public string MoveUpName { get; }

    /// <summary>Gets the accessible name of the Move down button.</summary>
    public string MoveDownName { get; }

    /// <summary>Gets the accessible name of the Review consent button.</summary>
    public string ReviewName { get; }

    private static string DescribeDegradation(HookBlockReason blockedBy, HookQuarantineEntry? quarantine) => blockedBy switch
    {
        HookBlockReason.PluginQuarantined when quarantine is not null =>
            $"Degraded: plugin {quarantine.PluginId} is quarantined ({quarantine.Reason}). This hook will not run.",
        HookBlockReason.AwaitingConsent =>
            "Degraded: awaiting consent for this exact command. This hook will not run until consent is approved.",
        _ => string.Empty,
    };
}
