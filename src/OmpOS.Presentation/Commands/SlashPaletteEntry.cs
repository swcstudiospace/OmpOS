namespace OmpOS.Presentation.Commands;

/// <summary>One row of the slash palette: a command, where it comes from, and whether the user can run it.</summary>
/// <param name="Name">The command name without the leading slash.</param>
/// <param name="Description">The text that tells the user what the command does; empty when the source gave none.</param>
/// <param name="InputHint">The hint for the text typed after the name, or <see langword="null"/> when the command takes no input.</param>
/// <param name="Source">Which source registered the command: the engine, the OmpOS catalogue, a Cursor command OmpOS cannot run, or a discovered command, skill or subagent.</param>
/// <param name="DisabledReason">The reason the command is disabled, or <see langword="null"/> when it is enabled.</param>
/// <param name="Scope">Where a discovered row's file lives; <see cref="SlashScope.BuiltIn"/> for the engine and the catalogue.</param>
/// <param name="Origin">The file a discovered row comes from, or <see langword="null"/>.</param>
public sealed record SlashPaletteEntry(
    string Name,
    string Description,
    string? InputHint,
    SlashPaletteSource Source,
    string? DisabledReason = null,
    SlashScope Scope = SlashScope.BuiltIn,
    string? Origin = null)
{
    /// <summary>Gets a value indicating whether the command can run. A disabled command stays in the list so its reason can be shown.</summary>
    public bool IsEnabled => DisabledReason is null;

    /// <summary>Gets the text to show for the name: the slash and the name.</summary>
    public string DisplayName => "/" + Name;

    /// <summary>Gets the text of the source badge: Engine, OmpOS, Cursor, Command, Skill or Subagent.</summary>
    public string SourceLabel => SlashPaletteCopy.SourceBadge(Source);

    /// <summary>Gets the text of the scope tag: <c>project</c> or <c>user</c>; empty for a built-in row.</summary>
    public string ScopeLabel => SlashPaletteCopy.ScopeTag(Scope);

    /// <summary>Gets a value indicating whether the row has a scope tag to show.</summary>
    public bool HasScope => Scope != SlashScope.BuiltIn;

    /// <summary>Gets a value indicating whether the row has a description to show.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>Gets a value indicating whether the row has an input hint to show.</summary>
    public bool HasInputHint => InputHint is { Length: > 0 };

    /// <summary>Gets the accessible name of the row: the command, what it does, its input hint, its source and its scope.</summary>
    public string AutomationName => SlashPaletteCopy.EntryName(this);

    /// <summary>Gets the order of the source when two rows share a name: the catalogue first, then the engine, a command, a skill and a subagent. It is the order the resolver tries them in.</summary>
    public int PrecedenceRank => Source switch
    {
        SlashPaletteSource.OmpOs or SlashPaletteSource.Cursor => 0,
        SlashPaletteSource.Engine => 1,
        SlashPaletteSource.Command => 2,
        SlashPaletteSource.Skill => 3,
        _ => 4,
    };

    /// <summary>Creates the row of a command of the OmpOS catalogue. Its input hint lists the arguments the descriptor declares, <c>&lt;name&gt;</c> for a required one and <c>[name]</c> for an optional one. A command OmpOS cannot run carries the Cursor badge.</summary>
    /// <param name="command">The registered command.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static SlashPaletteEntry FromCatalogue(SlashCommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hint = command.Arguments.Count == 0
            ? null
            : string.Join(' ', command.Arguments.Select(static argument => argument.IsRequired ? $"<{argument.Name}>" : $"[{argument.Name}]"));
        var source = command.Status == SlashCommandStatus.Unavailable ? SlashPaletteSource.Cursor : SlashPaletteSource.OmpOs;
        return new SlashPaletteEntry(command.Name, command.Description, hint, source, command.DisabledReason);
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
            SlashPaletteSource.Engine);
    }

    /// <summary>Creates the row of a discovered custom command.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static SlashPaletteEntry FromDiscovered(SlashDiscoveredCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new SlashPaletteEntry(command.Name, command.Description, EmptyToNull(command.ArgumentHint), SlashPaletteSource.Command, null, command.Scope, command.Origin);
    }

    /// <summary>Creates the row of a discovered skill.</summary>
    /// <param name="skill">The skill.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="skill"/> is null.</exception>
    public static SlashPaletteEntry FromDiscovered(SlashDiscoveredSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return new SlashPaletteEntry(skill.Name, skill.Description, SlashPaletteCopy.SkillHint, SlashPaletteSource.Skill, null, skill.Scope, skill.Origin);
    }

    /// <summary>Creates the row of a discovered subagent.</summary>
    /// <param name="subagent">The subagent.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subagent"/> is null.</exception>
    public static SlashPaletteEntry FromDiscovered(SlashDiscoveredSubagent subagent)
    {
        ArgumentNullException.ThrowIfNull(subagent);
        return new SlashPaletteEntry(subagent.Name, subagent.Description, SlashPaletteCopy.SubagentHint, SlashPaletteSource.Subagent, null, subagent.Scope, subagent.Origin);
    }

    private static string? EmptyToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
