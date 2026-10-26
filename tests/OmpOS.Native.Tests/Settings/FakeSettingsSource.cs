using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings;

/// <summary>
/// The scripted half every settings source fake shares: the values it returns, an optional failure for a load or a save, a gate that holds a
/// load until the test releases it, and a record of what was saved. Nothing sleeps; a gate is released by the test.
/// </summary>
/// <typeparam name="TValues">The values record the source reads and writes.</typeparam>
internal abstract class FakeSettingsSource<TValues>
    where TValues : class
{
    /// <summary>Creates a source that returns <paramref name="values"/>.</summary>
    /// <param name="values">The values a load returns.</param>
    protected FakeSettingsSource(TValues values)
    {
        Values = values;
    }

    /// <summary>Gets or sets the values a load returns. A successful save replaces them.</summary>
    public TValues Values { get; set; }

    /// <summary>Gets or sets the reason every load fails with, or null for loads that succeed.</summary>
    public string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason every save fails with, or null for saves that succeed.</summary>
    public string? SaveFailure { get; set; }

    /// <summary>Gets or sets a gate; while it is set, a load does not finish until the test completes it.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets how many loads were started.</summary>
    public int LoadCount { get; private set; }

    /// <summary>Gets the values that were saved, in order.</summary>
    public List<TValues> Saved { get; } = [];

    /// <summary>Reads the values the way a source does: after the gate, failing with the scripted reason.</summary>
    /// <param name="cancellationToken">Cancels the wait for the gate.</param>
    /// <returns>The values.</returns>
    protected async Task<TValues> LoadCoreAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return LoadFailure is { } failure ? throw new SettingsSourceException(failure) : Values;
    }

    /// <summary>Writes the values the way a source does, failing with the scripted reason.</summary>
    /// <param name="values">The values to save.</param>
    /// <returns>A completed task.</returns>
    protected Task SaveCoreAsync(TValues values)
    {
        if (SaveFailure is { } failure)
        {
            throw new SettingsSourceException(failure);
        }

        Saved.Add(values);
        Values = values;
        return Task.CompletedTask;
    }
}
