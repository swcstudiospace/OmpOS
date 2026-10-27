namespace OmpOS.Presentation.Commands;

/// <summary>One row of the slash palette: a command, where it comes from, and whether the user can run it.</summary>
/// <param name="Name">The command name without the leading slash.</param>
/// <param name="Description">The text that tells the user what the command does; empty when the source gave none.</param>
/// <param name="InputHint">The hint for the text typed after the name, or <see langword="null"/> when the command takes no input.</param>
/// <param name="Source">Whether the engine or the OmpOS catalogue registered the command.</param>
/// <param name="DisabledReason">The reason the command is disabled, or <see langword="null"/> when it is enabled.</param>
public sealed record SlashPaletteEntry(string Name, string Description, string? InputHint, SlashPaletteSource Source, string? DisabledReason = null)
{
    /// <summary>Gets a value indicating whether the command can run. A disabled command stays in the list so its reason can be shown.</summary>
    public bool IsEnabled => DisabledReason is null;

    /// <summary>Gets the text to show for the name: the slash and the name.</summary>
    public string DisplayName => "/" + Name;

    /// <summary>Gets the text of the source tag: <see cref="SlashPaletteCopy.SourceOmp"/> or <see cref="SlashPaletteCopy.SourceOmpOs"/>.</summary>
    public string SourceLabel => Source == SlashPaletteSource.Omp ? SlashPaletteCopy.SourceOmp : SlashPaletteCopy.SourceOmpOs;

    /// <summary>Gets a value indicating whether the row has a description to show.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>Gets a value indicating whether the row has an input hint to show.</summary>
    public bool HasInputHint => InputHint is { Length: > 0 };

    /// <summary>Gets the accessible name of the row: the command, what it does, its input hint and its source.</summary>
    public string AutomationName => SlashPaletteCopy.EntryName(this);

    /// <summary>Creates the row of a command of the OmpOS catalogue. Its input hint lists the arguments the descriptor declares, <c>&lt;name&gt;</c> for a required one and <c>[name]</c> for an optional one.</summary>
    /// <param name="command">The registered command.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static SlashPaletteEntry FromCatalogue(SlashCommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hint = command.Arguments.Count == 0
            ? null
            : string.Join(' ', command.Arguments.Select(static argument => argument.IsRequired ? $"<{argument.Name}>" : $"[{argument.Name}]"));
        return new SlashPaletteEntry(command.Name, command.Description, hint, SlashPaletteSource.OmpOs, command.DisabledReason);
    }

    /// <summary>Creates the row of a command the engine advertises.</summary>
    /// <param name="command">The advertised command.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static SlashPaletteEntry FromEngine(SlashEngineCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new SlashPaletteEntry(
            command.Name,
            command.Description?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(command.InputHint) ? null : command.InputHint.Trim(),
            SlashPaletteSource.Omp);
    }
}
