using OmpOS.Presentation.Composer;

namespace OmpOS.Native.Views.Sessions;

/// <summary>Builds the slash action targets that a session's own composer can reach (its mode chip, its model picker and its slash palette), on top of the targets the head supplied.</summary>
internal static class ComposerSlashTargets
{
    /// <summary>Adds the composer's targets to the head's. A target the head already supplied is kept; a composer without a mode chip or model picker leaves that target unset.</summary>
    /// <param name="composer">The composer of the session.</param>
    /// <param name="head">The targets the head supplied, or <see langword="null"/>.</param>
    /// <returns>The combined targets.</returns>
    public static DesktopSlashTargets Create(ComposerViewModel composer, DesktopSlashTargets? head)
    {
        ArgumentNullException.ThrowIfNull(composer);

        var targets = head ?? new DesktopSlashTargets();
        return targets with
        {
            SetMode = targets.SetMode ?? ((mode, cancellationToken) => SetModeAsync(composer, mode, cancellationToken)),
            PickModel = targets.PickModel ?? ((filter, cancellationToken) => PickModelAsync(composer, filter, cancellationToken)),
            ShowHelp = targets.ShowHelp ?? (text => composer.Draft = "/" + text),
        };
    }

    /// <summary>Switches the mode chip to the choice named <paramref name="mode"/>.</summary>
    /// <param name="composer">The composer.</param>
    /// <param name="mode">The mode name, matched ignoring case against the value and the name of each choice.</param>
    /// <param name="cancellationToken">Cancels the wait for the engine.</param>
    /// <returns><see langword="null"/> when the mode is now in use, or the reason it is not.</returns>
    internal static async Task<string?> SetModeAsync(ComposerViewModel composer, string mode, CancellationToken cancellationToken)
    {
        var chip = composer.ModeChip;
        if (chip is null || chip.Choices.Count == 0)
        {
            return "This session does not offer modes.";
        }

        var choice = chip.Choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, mode, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.Name, mode, StringComparison.OrdinalIgnoreCase));
        if (choice is null)
        {
            return "The engine offers no " + mode + " mode. It offers: " + string.Join(", ", chip.Choices.Select(static offered => offered.Value)) + ".";
        }

        cancellationToken.ThrowIfCancellationRequested();
        await chip.SelectAsync(choice).ConfigureAwait(true);
        return string.Equals(chip.CurrentValue, choice.Value, StringComparison.Ordinal) ? null : chip.Notice ?? "The mode did not change.";
    }

    /// <summary>Filters the model picker by <paramref name="filter"/> and selects the model when exactly one matches.</summary>
    /// <param name="composer">The composer.</param>
    /// <param name="filter">The filter words; blank shows every model.</param>
    /// <param name="cancellationToken">Cancels the wait for the model list.</param>
    /// <returns>The sentence to show.</returns>
    internal static async Task<string> PickModelAsync(ComposerViewModel composer, string filter, CancellationToken cancellationToken)
    {
        var picker = composer.ModelPicker;
        if (picker is null)
        {
            return "This session does not offer a model picker.";
        }

        await picker.EnsureLoadedAsync(cancellationToken).ConfigureAwait(true);
        picker.FilterText = filter;
        var matches = picker.Rows.Where(static row => row.Choice is not null).Select(static row => row.Choice!).ToArray();
        if (matches.Length == 1)
        {
            await picker.SelectAsync(matches[0]).ConfigureAwait(true);
            picker.FilterText = string.Empty;
            return picker.Notice ?? "Model: " + matches[0].Name + ".";
        }

        return matches.Length == 0
            ? "No model matches '" + filter + "'."
            : "Several models match '" + filter + "': pick one in the model list.";
    }
}
