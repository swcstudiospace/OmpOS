using OmpOS.Presentation.Commands;

namespace OmpOS.Presentation.Settings.Customize;

/// <summary>Lists the slash commands of a <see cref="SlashCommandRegistry"/>: the catalogue the composer's slash palette offers.</summary>
public sealed class SlashCommandsSettingsSource : ICommandsSettingsSource
{
    private readonly SlashCommandRegistry _registry;

    /// <summary>Initializes a new instance of the <see cref="SlashCommandsSettingsSource"/> class.</summary>
    /// <param name="registry">The registry whose commands are listed.</param>
    public SlashCommandsSettingsSource(SlashCommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken)
    {
        var entries = _registry.ListAll().Select(ToEntry).ToArray();
        return Task.FromResult(new CustomizeCatalog(entries, []));
    }

    private static CustomizeEntry ToEntry(SlashCommandDescriptor command) =>
        new(
            Title(command),
            CustomizeScope.BuiltIn,
            CustomizeCopy.CommandOrigin(command.Platforms),
            string.IsNullOrWhiteSpace(command.DisabledReason)
                ? command.Description
                : CustomizeCopy.DisabledDescription(command.Description, command.DisabledReason));

    private static string Title(SlashCommandDescriptor command)
    {
        var arguments = command.Arguments.Select(argument => argument.IsRequired ? $"<{argument.Name}>" : $"[{argument.Name}]");
        return string.Join(' ', new[] { $"/{command.Name}" }.Concat(arguments));
    }
}
