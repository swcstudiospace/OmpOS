using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>A General source the test scripts: it holds values in memory, counts writes, and can hold a load open or fail a load or a write.</summary>
internal sealed class ScriptedGeneralSource : IGeneralSettingsSource
{
    private readonly TaskCompletionSource _loadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Creates a source whose load completes at once.</summary>
    /// <param name="values">The values the source holds.</param>
    internal ScriptedGeneralSource(GeneralSettingsValues values)
    {
        Values = values;
        _loadGate.SetResult();
    }

    /// <summary>Creates a source whose load stays pending until <see cref="CompleteLoad"/> is called.</summary>
    internal ScriptedGeneralSource()
    {
        Values = GeneralSettingsValues.Defaults;
    }

    /// <summary>Gets the values the source holds; a successful write replaces them.</summary>
    internal GeneralSettingsValues Values { get; private set; }

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason writes fail with, or null when they succeed.</summary>
    internal string? SaveFailure { get; set; }

    /// <summary>Gets the number of writes that reached the source.</summary>
    internal int SaveCalls { get; private set; }

    /// <summary>Lets a held load complete.</summary>
    internal void CompleteLoad() => _loadGate.TrySetResult();

    /// <inheritdoc/>
    public async Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken)
    {
        await _loadGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (LoadFailure is { } failure)
        {
            throw new SettingsSourceException(failure);
        }

        return Values;
    }

    /// <inheritdoc/>
    public Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken)
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
