using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Ignore;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>An Ignore source the test scripts: it holds values in memory, counts writes, and can fail a load, a file listing or a write.</summary>
internal sealed class ScriptedIgnoreSource : IIgnoreSettingsSource
{
    /// <summary>Gets the values the source holds; a successful write replaces them.</summary>
    internal IgnoreSettingsValues Values { get; private set; } = IgnoreSettingsValues.Defaults;

    /// <summary>Gets or sets the files the source reports.</summary>
    internal IReadOnlyList<IgnoreFileRow> Files { get; set; } = [];

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason writes fail with, or null when they succeed.</summary>
    internal string? SaveFailure { get; set; }

    /// <summary>Gets how many writes succeeded or were attempted.</summary>
    internal int SaveCalls { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<string> DefaultPatterns { get; } = [".git/", "node_modules/", "*.png"];

    /// <summary>Replaces the saved values without counting a write.</summary>
    /// <param name="values">The values.</param>
    internal void Seed(IgnoreSettingsValues values) => Values = values;

    /// <inheritdoc/>
    public Task<IgnoreSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is { } failure
            ? Task.FromException<IgnoreSettingsValues>(new SettingsSourceException(failure))
            : Task.FromResult(Values);

    /// <inheritdoc/>
    public Task SaveAsync(IgnoreSettingsValues values, CancellationToken cancellationToken)
    {
        SaveCalls++;
        if (SaveFailure is { } failure)
        {
            return Task.FromException(new SettingsSourceException(failure));
        }

        Values = values;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IgnoreFileRow>> FoundFilesAsync(CancellationToken cancellationToken) => Task.FromResult(Files);
}
