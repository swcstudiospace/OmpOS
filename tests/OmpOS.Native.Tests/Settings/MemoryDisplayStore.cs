using OmpOS.Presentation.Commands.Handlers;

namespace OmpOS.Native.Tests.Settings;

/// <summary>An in-memory display settings store for the Display page tests: it keeps what was saved, counts the writes and can refuse them.</summary>
internal sealed class MemoryDisplayStore : IDisplaySettingsStore
{
    /// <summary>Gets or sets the values a load returns and a save replaces.</summary>
    internal DisplaySettingsValues Values { get; set; } = DisplaySettingsValues.Defaults;

    /// <summary>Gets or sets the failure every load throws, or null.</summary>
    internal Exception? LoadFailure { get; set; }

    /// <summary>Gets or sets the failure every save throws, or null.</summary>
    internal Exception? SaveFailure { get; set; }

    /// <summary>Gets the values of every successful save, in order.</summary>
    internal List<DisplaySettingsValues> Saves { get; } = [];

    /// <inheritdoc/>
    public Task<DisplaySettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is { } failure ? Task.FromException<DisplaySettingsValues>(failure) : Task.FromResult(Values);

    /// <inheritdoc/>
    public Task SaveAsync(DisplaySettingsValues values, CancellationToken cancellationToken)
    {
        if (SaveFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        Values = values;
        Saves.Add(values);
        return Task.CompletedTask;
    }
}
