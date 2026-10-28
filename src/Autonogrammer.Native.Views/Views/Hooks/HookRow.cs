using System.Windows.Input;
using Autonogrammer.Harness.Plugins;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// One hook in the list, as the view shows it: its trigger, exact command, enabled and consent state, and a degraded marker
/// when the hook cannot run. Each action is a command on the model that owns the list.
/// </summary>
internal sealed class HookRow
{
    /// <summary>Creates the row for one hook.</summary>
    /// <param name="owner">The model that applies the row's actions.</param>
    /// <param name="hook">The hook.</param>
    /// <param name="consented">Whether consent is recorded for the hook's exact command.</param>
    /// <param name="quarantine">The quarantine of the hook's plugin, or null when the plugin is not quarantined.</param>
    /// <param name="selected">Whether the consent panel shows this hook.</param>
    public HookRow(HooksModel owner, HookRegistration hook, bool consented, PluginQuarantine? quarantine, bool selected)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(hook);

        Id = hook.Id;
        PluginId = hook.PluginId;
        Arguments = hook.Command.Arguments.ToArray();
        WorkingDirectory = hook.Command.WorkingDirectory;
        IsEnabled = hook.Enabled;
        IsConsented = consented;
        IsSelected = selected;

        // The runner checks quarantine before consent, so a quarantined plugin's hook reports the quarantine.
        BlockedBy = quarantine is not null
            ? HookExecutionStatus.PluginQuarantined
            : consented ? null : HookExecutionStatus.AwaitingConsent;

        TriggerText = "Trigger: " + ManifestTriggerName(hook.Trigger);
        CommandText = "Command: " + HookArgumentText.CommandLine(Arguments);
        WorkingDirectoryText = "Working directory: " + WorkingDirectory;
        ArgumentListText = HookArgumentText.Numbered(Arguments);
        EnabledText = IsEnabled ? "Enabled" : "Disabled";
        ConsentText = IsConsented ? "Consent recorded for this exact command" : "Consent not recorded for this exact command";
        ReviewText = IsSelected ? "Reviewing" : "Review consent";
        DegradedText = DescribeDegradation(BlockedBy, quarantine);

        EnableName = "Enable hook " + Id;
        DisableName = "Disable hook " + Id;
        MoveUpName = "Move up hook " + Id;
        MoveDownName = "Move down hook " + Id;
        ReviewName = "Review consent for hook " + Id;

        EnableCommand = new HookActionCommand(() => owner.SetEnabledAsync(Id, enabled: true));
        DisableCommand = new HookActionCommand(() => owner.SetEnabledAsync(Id, enabled: false));
        MoveUpCommand = new HookActionCommand(() => owner.MoveUpAsync(Id));
        MoveDownCommand = new HookActionCommand(() => owner.MoveDownAsync(Id));
        ReviewCommand = new HookActionCommand(() =>
        {
            owner.Select(Id);
            return Task.CompletedTask;
        });
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

    /// <summary>
    /// Gets the status the runner reports for this hook while it cannot run: <see cref="HookExecutionStatus.PluginQuarantined"/>
    /// or <see cref="HookExecutionStatus.AwaitingConsent"/>. It is null when the hook is not blocked.
    /// </summary>
    public HookExecutionStatus? BlockedBy { get; }

    /// <summary>Gets whether the hook is awaiting consent or belongs to a quarantined plugin. Such a row carries the degraded marker.</summary>
    public bool IsDegraded => BlockedBy is not null;

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

    /// <summary>Gets the command that enables the hook.</summary>
    public ICommand EnableCommand { get; }

    /// <summary>Gets the command that disables the hook.</summary>
    public ICommand DisableCommand { get; }

    /// <summary>Gets the command that moves the hook one place earlier in run order.</summary>
    public ICommand MoveUpCommand { get; }

    /// <summary>Gets the command that moves the hook one place later in run order.</summary>
    public ICommand MoveDownCommand { get; }

    /// <summary>Gets the command that shows this hook in the consent panel.</summary>
    public ICommand ReviewCommand { get; }

    private static string ManifestTriggerName(HookTrigger trigger)
    {
        var name = trigger.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string DescribeDegradation(HookExecutionStatus? blockedBy, PluginQuarantine? quarantine) => blockedBy switch
    {
        HookExecutionStatus.PluginQuarantined when quarantine is not null =>
            $"Degraded: plugin {quarantine.PluginId} is quarantined ({quarantine.Reason}). This hook will not run.",
        HookExecutionStatus.AwaitingConsent =>
            "Degraded: awaiting consent for this exact command. This hook will not run until consent is approved.",
        _ => string.Empty,
    };
}
