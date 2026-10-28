namespace OmpOS.Presentation.Hooks;

/// <summary>
/// What the user reviews before approving one hook: the exact command or arguments, whether it runs through a shell, the matcher, the timeout, the
/// names of the environment variables it adds, its failure policy, and the difference from the version they approved before. Nothing is hidden or shortened.
/// </summary>
public sealed class HookConsentPrompt
{
    /// <summary>Creates the prompt for one hook.</summary>
    /// <param name="item">The hook as it is now.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public HookConsentPrompt(HookItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        Changes = HookDiff.Changes(item, item.LastApproved);
    }

    /// <summary>Gets the hook under review.</summary>
    public HookItem Item { get; }

    /// <summary>Gets the heading: the hook's name.</summary>
    public string Title => Item.DisplayName;

    /// <summary>Gets the subtitle: the event and the file the hook comes from.</summary>
    public string Source => $"{Item.EventName} in {Item.SourceFile}";

    /// <summary>
    /// Gets the exact thing that runs, verbatim: the shell string, the numbered arguments of a hook that runs without a shell, or the prompt text of a prompt hook.
    /// </summary>
    public string ExactText => Item.IsPrompt
        ? Item.Prompt ?? string.Empty
        : Item.Command is { } command ? command : Item.Arguments is { } arguments ? HookCommandText.Numbered(arguments) : string.Empty;

    /// <summary>Gets the label of <see cref="ExactText"/>: the command, the arguments or the prompt.</summary>
    public string ExactLabel => Item.IsPrompt ? HooksCopy.PromptLabel : Item.Command is not null ? HooksCopy.CommandLabel : HooksCopy.ArgumentsLabel;

    /// <summary>Gets the sentence that says whether the hook runs through a shell, without one, or asks a model.</summary>
    public string ShellText => HooksCopy.ShellText(Item);

    /// <summary>Gets the matcher text, or the sentence that says it matches everything.</summary>
    public string MatcherText => HooksCopy.MatcherText(Item.Matcher);

    /// <summary>Gets the timeout text.</summary>
    public string TimeoutText => HooksCopy.TimeoutText(Item.TimeoutSeconds);

    /// <summary>Gets the names of the extra environment variables, never their values.</summary>
    public string EnvironmentText => HooksCopy.EnvironmentText(Item.EnvKeys);

    /// <summary>Gets the sentence that says what happens when the hook fails.</summary>
    public string FailurePolicyText => Item.FailClosed ? HooksCopy.FailsClosed : HooksCopy.FailsOpen;

    /// <summary>Gets the directory the hook runs in, relative to its scope root.</summary>
    public string WorkingDirectory => Item.WorkingDirectory;

    /// <summary>Gets the warning that comes with every approval.</summary>
    public string Warning { get; } = HooksCopy.ConsentWarning;

    /// <summary>Gets the fields that differ from the version approved before. Empty when the hook was never approved or is unchanged.</summary>
    public IReadOnlyList<HookFieldChange> Changes { get; }

    /// <summary>Gets a value indicating whether the user approved an earlier version of this hook.</summary>
    public bool WasApprovedBefore => Item.LastApproved is not null;

    /// <summary>Gets the sentence about the comparison: never approved, nothing changed, or the heading of the change list.</summary>
    public string ChangesSummary => !WasApprovedBefore
        ? HooksCopy.NeverApproved
        : Changes.Count == 0 ? HooksCopy.NoChanges : HooksCopy.ChangesLabel;

    /// <summary>Gets a value indicating whether the hook can be approved now: it has no consent yet.</summary>
    public bool CanApprove => !Item.IsConsented && Item.IsKnownEvent && Item.IsApplicable;

    /// <summary>Gets a value indicating whether the approval can be withdrawn: the hook is consented to and is not pre-consented.</summary>
    public bool CanRevoke => Item.IsConsented && !Item.IsPreConsented;
}
