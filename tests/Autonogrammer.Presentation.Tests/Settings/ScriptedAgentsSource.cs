using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>An Agents source the test scripts: it holds values in memory, counts writes, and can fail a load or a write.</summary>
internal sealed class ScriptedAgentsSource : IAgentsSettingsSource
{
    /// <summary>Gets the values the source holds; a successful write replaces them.</summary>
    internal AgentsSettingsValues Values { get; private set; } = AgentsSettingsValues.Defaults;

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason writes fail with, or null when they succeed.</summary>
    internal string? SaveFailure { get; set; }

    /// <summary>Gets the number of writes that reached the source.</summary>
    internal int SaveCalls { get; private set; }

    /// <inheritdoc/>
    public Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is { } failure
            ? Task.FromException<AgentsSettingsValues>(new SettingsSourceException(failure))
            : Task.FromResult(Values);

    /// <inheritdoc/>
    public Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken)
    {
        SaveCalls++;
        if (SaveFailure is { } failure)
        {
            return Task.FromException(new SettingsSourceException(failure));
        }

        Values = values;
        return Task.CompletedTask;
    }
}
