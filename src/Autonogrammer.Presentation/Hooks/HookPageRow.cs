namespace Autonogrammer.Presentation.Hooks;

/// <summary>One hook in the Hooks page's list: the hook, the one state it is in, and the short texts the row shows. The row is immutable.</summary>
public sealed class HookPageRow
{
    private const int PreviewLength = 120;

    /// <summary>Creates the row of one hook.</summary>
    /// <param name="item">The hook.</param>
    /// <param name="isRunning">Whether the hook is running now.</param>
    /// <param name="isSelected">Whether the consent prompt shows this hook.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public HookPageRow(HookItem item, bool isRunning, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        IsSelected = isSelected;
        State = StateOf(item, isRunning);
    }

    /// <summary>Gets the hook.</summary>
    public HookItem Item { get; }

    /// <summary>Gets the hook's identifier.</summary>
    public string Id => Item.Id;

    /// <summary>Gets a value indicating whether the consent prompt shows this hook.</summary>
    public bool IsSelected { get; }

    /// <summary>Gets the one state the row is in.</summary>
    public HookRowState State { get; }

    /// <summary>Gets the word for <see cref="State"/>.</summary>
    public string StateText => HooksCopy.StateText(State);

    /// <summary>Gets the sentence that explains <see cref="State"/>.</summary>
    public string StateDetail => HooksCopy.StateDetail(State, Item);

    /// <summary>Gets the name of the hook.</summary>
    public string Name => Item.DisplayName;

    /// <summary>Gets the event the hook runs for.</summary>
    public string EventName => Item.EventName;

    /// <summary>Gets the matcher text, or the sentence that says it matches everything.</summary>
    public string MatcherText => HooksCopy.MatcherText(Item.Matcher);

    /// <summary>Gets the type of the hook: <c>command</c> or <c>prompt</c>.</summary>
    public string TypeText => Item.IsPrompt ? "prompt" : "command";

    /// <summary>Gets the command, the arguments or the prompt on one line, cut at 120 characters. The prompt shows the whole text.</summary>
    public string Preview
    {
        get
        {
            var text = Item.IsPrompt
                ? Item.Prompt ?? string.Empty
                : Item.Command ?? (Item.Arguments is { } arguments ? HookCommandText.CommandLine(arguments) : string.Empty);
            var line = text.ReplaceLineEndings(" ");
            return line.Length <= PreviewLength ? line : line[..PreviewLength] + "...";
        }
    }

    /// <summary>Gets the sentence that says what happens when the hook fails.</summary>
    public string FailurePolicyText => Item.FailClosed ? HooksCopy.FailsClosed : HooksCopy.FailsOpen;

    /// <summary>Gets the last-run text: the outcome and how long it took, or that the hook has not run.</summary>
    public string LastRunText => HooksCopy.LastRunText(Item.LastRun);

    /// <summary>Gets the accessible name of the row.</summary>
    public string AutomationName => HooksCopy.RowName(Item.EventName, Item.DisplayName, StateText);

    /// <summary>Gets the accessible name of the button that opens the consent prompt of the hook.</summary>
    public string ReviewAutomationName => "Review " + Item.DisplayName;

    /// <summary>Gets the accessible name of the switch that turns the hook on or off.</summary>
    public string ToggleAutomationName => HooksCopy.EnabledLabel + ": " + Item.DisplayName;

    /// <summary>Gets a value indicating whether the user can switch the hook on or off: Autonogrammer can produce its event.</summary>
    public bool CanToggle => Item.IsKnownEvent && Item.IsApplicable;

    /// <summary>Gets a value indicating whether the row asks for the user's attention: the hook needs approval, is quarantined or failed.</summary>
    public bool NeedsAttention => State is HookRowState.NeedsConsent or HookRowState.Quarantined or HookRowState.LastRunFailed;

    private static HookRowState StateOf(HookItem item, bool isRunning)
    {
        if (!item.IsApplicable)
        {
            return HookRowState.NotApplicable;
        }

        if (!item.IsKnownEvent)
        {
            return HookRowState.UnknownEvent;
        }

        if (item.QuarantineReason is not null)
        {
            return HookRowState.Quarantined;
        }

        if (!item.IsEnabled)
        {
            return HookRowState.Disabled;
        }

        if (!item.IsConsented)
        {
            return HookRowState.NeedsConsent;
        }

        if (isRunning)
        {
            return HookRowState.Running;
        }

        return item.LastRun is { Outcome: HookRunOutcome.Failed or HookRunOutcome.TimedOut or HookRunOutcome.Faulted or HookRunOutcome.InvalidResponse }
            ? HookRowState.LastRunFailed
            : HookRowState.Ready;
    }
}
