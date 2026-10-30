namespace Autonogrammer.Presentation.Hooks;

/// <summary>Compares a hook with the version the user last approved, field by field.</summary>
public static class HookDiff
{
    /// <summary>
    /// Lists the fields of <paramref name="item"/> that differ from <paramref name="approved"/>. With no approved version the list is empty (see
    /// <see cref="HookItem.LastApproved"/>: the prompt says the hook was never approved).
    /// </summary>
    /// <param name="item">The hook as it is now.</param>
    /// <param name="approved">The approved version, or null.</param>
    /// <returns>The changed fields, in a fixed order; empty when nothing changed or there is no approved version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public static IReadOnlyList<HookFieldChange> Changes(HookItem item, HookApprovedVersion? approved)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (approved is null)
        {
            return [];
        }

        var changes = new List<HookFieldChange>();
        Add(changes, HooksCopy.CommandLabel, Show(approved.Command), Show(item.Command));
        Add(changes, HooksCopy.ArgumentsLabel, Show(approved.Arguments), Show(item.Arguments));
        Add(changes, HooksCopy.PromptLabel, Show(approved.Prompt), Show(item.Prompt));
        Add(changes, "Shell", Show(approved.ShellName), Show(item.ShellName));
        Add(changes, HooksCopy.MatcherLabel, HooksCopy.MatcherText(approved.Matcher), HooksCopy.MatcherText(item.Matcher));
        Add(changes, HooksCopy.TimeoutLabel, HooksCopy.TimeoutText(approved.TimeoutSeconds), HooksCopy.TimeoutText(item.TimeoutSeconds));
        Add(changes, HooksCopy.FailurePolicyLabel, FailureText(approved.FailClosed), FailureText(item.FailClosed));
        Add(changes, HooksCopy.EnvironmentLabel, HooksCopy.EnvironmentText(approved.EnvKeys), HooksCopy.EnvironmentText(item.EnvKeys));
        Add(changes, HooksCopy.WorkingDirectoryLabel, approved.WorkingDirectory, item.WorkingDirectory);
        return changes;
    }

    private static void Add(List<HookFieldChange> changes, string field, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new HookFieldChange(field, before, after));
        }
    }

    private static string FailureText(bool failClosed) => failClosed ? HooksCopy.FailsClosed : HooksCopy.FailsOpen;

    private static string Show(string? value) => string.IsNullOrEmpty(value) ? "(none)" : value;

    private static string Show(IReadOnlyList<string>? values) => values is null ? "(none)" : HookCommandText.CommandLine(values);
}
