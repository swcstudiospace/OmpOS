using Autonogrammer.Contracts.Controls;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.Services;

/// <summary>Maps what an engine session reports to the wire shapes of the configuration and command endpoints.</summary>
internal static class ConfigOptionMapper
{
    /// <summary>The identifier of the model option the API reports.</summary>
    public const string ModelOptionId = "model";

    /// <summary>Reads the options of a session: the engine's select options, and the model as one more option when the engine offers models.</summary>
    /// <param name="session">The engine session.</param>
    /// <returns>The options in the engine's order, the model last.</returns>
    public static IReadOnlyList<ConfigOption> OptionsOf(object session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var options = new List<ConfigOption>();
        if (session is IOmpSessionOptions engineOptions)
        {
            foreach (var option in engineOptions.Options)
            {
                options.Add(new ConfigOption
                {
                    Id = option.Id,
                    Name = option.Name,
                    Category = option.Category,
                    CurrentValue = option.CurrentValue,
                    Values = [.. option.Values.Select(static value => new ConfigOptionValue { Value = value.Value, Name = value.Name, Description = value.Description })],
                });
            }
        }

        if (session is IOmpModelControl { ModelChoices.Count: > 0 } models
            && !options.Exists(static existing => string.Equals(existing.Id, ModelOptionId, StringComparison.Ordinal) || string.Equals(existing.Category, ModelOptionId, StringComparison.Ordinal)))
        {
            options.Add(new ConfigOption
            {
                Id = ModelOptionId,
                Name = "Model",
                Category = ModelOptionId,
                CurrentValue = models.CurrentModelId ?? string.Empty,
                Values = [.. models.ModelChoices.Select(static choice => new ConfigOptionValue { Value = choice.Value, Name = choice.Name })],
            });
        }

        return options;
    }

    /// <summary>Reads the slash commands a session advertises.</summary>
    /// <param name="session">The engine session.</param>
    /// <returns>The commands in the engine's order; empty when the session advertises none.</returns>
    public static IReadOnlyList<SessionCommand> CommandsOf(object session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session is IOmpSessionCommands commands
            ? [.. commands.Commands.Select(static command => new SessionCommand { Name = command.Name, Description = command.Description, InputHint = command.InputHint })]
            : [];
    }

    /// <summary>Gets whether the session offers any control surface at all.</summary>
    /// <param name="session">The engine session.</param>
    /// <returns>True when it implements options, model control, commands or live steering.</returns>
    public static bool HasControls(object session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session is IOmpSessionOptions or IOmpModelControl or IOmpSessionCommands or IOmpLiveSteering;
    }
}
