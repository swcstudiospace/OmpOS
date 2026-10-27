using OmpOS.Presentation.Commands;

namespace OmpOS.Presentation.Settings.Customize;

/// <summary>
/// Lists the slash commands of a <see cref="SlashCommandRegistry"/> (the catalogue the composer's slash palette offers) and, when a discovery source is given, the custom
/// commands and subagents that discovery found in the workspace and in the user's home folder, each with its file and scope. Files discovery could not use are the page's problems.
/// </summary>
public sealed class SlashCommandsSettingsSource : ICommandsSettingsSource
{
    private readonly SlashCommandRegistry _registry;
    private readonly ISlashDiscoverySource? _discovery;

    /// <summary>Initializes a new instance of the <see cref="SlashCommandsSettingsSource"/> class.</summary>
    /// <param name="registry">The registry whose commands are listed.</param>
    /// <param name="discovery">The source of the discovered commands and subagents, or <see langword="null"/> to list the registry only.</param>
    public SlashCommandsSettingsSource(SlashCommandRegistry registry, ISlashDiscoverySource? discovery = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _discovery = discovery;
    }

    /// <inheritdoc/>
    public async Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        var entries = _registry.ListAll().Select(ToEntry).ToList();
        IReadOnlyList<string> problems = [];
        if (_discovery is not null)
        {
            var found = await _discovery.LoadAsync(cancellationToken).ConfigureAwait(false);
            entries.AddRange(found.Commands.Select(ToEntry));
            entries.AddRange(found.Subagents.Select(ToEntry));
            problems = found.Problems;
        }

        return new CustomizeCatalog(entries, problems);
    }

    private static CustomizeEntry ToEntry(SlashCommandDescriptor command) =>
        new(
            Title(command),
            CustomizeScope.BuiltIn,
            CustomizeCopy.CommandOrigin(command.Platforms),
            string.IsNullOrWhiteSpace(command.DisabledReason)
                ? command.Description
                : CustomizeCopy.DisabledDescription(command.Description, command.DisabledReason));

    private static CustomizeEntry ToEntry(SlashDiscoveredCommand command) =>
        new(
            command.ArgumentHint is { Length: > 0 } ? $"/{command.Name} {command.ArgumentHint}" : "/" + command.Name,
            ToScope(command.Scope),
            command.Origin,
            command.Description);

    private static CustomizeEntry ToEntry(SlashDiscoveredSubagent subagent) =>
        new("/" + subagent.Name + " <task>", ToScope(subagent.Scope), subagent.Origin, CustomizeCopy.SubagentDescription(subagent.Description));

    private static CustomizeScope ToScope(SlashScope scope) => scope == SlashScope.User ? CustomizeScope.User : CustomizeScope.Project;

    private static string Title(SlashCommandDescriptor command)
    {
        var arguments = command.Arguments.Select(argument => argument.IsRequired ? $"<{argument.Name}>" : $"[{argument.Name}]");
        return string.Join(' ', new[] { $"/{command.Name}" }.Concat(arguments));
    }
}
