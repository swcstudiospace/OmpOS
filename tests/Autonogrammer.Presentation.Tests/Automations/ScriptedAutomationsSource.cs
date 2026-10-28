using Autonogrammer.Presentation.Automations;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>
/// An automations source the test scripts: it holds the automations in memory, applies enable and resume writes to them, counts each
/// write, and can hold a load open or fail a load or a write with the expected failure.
/// </summary>
internal sealed class ScriptedAutomationsSource : IAutomationsSource
{
    private readonly List<AutomationEntry> _automations = [];
    private readonly TaskCompletionSource _loadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Creates a source whose load completes at once.</summary>
    /// <param name="automations">The automations.</param>
    internal ScriptedAutomationsSource(params AutomationEntry[] automations)
    {
        _automations.AddRange(automations);
        _loadGate.SetResult();
    }

    /// <summary>Creates a source whose load stays pending until <see cref="CompleteLoad"/> is called.</summary>
    /// <param name="holdLoad">True to hold the load open.</param>
    /// <param name="automations">The automations.</param>
    internal ScriptedAutomationsSource(bool holdLoad, params AutomationEntry[] automations)
    {
        _automations.AddRange(automations);
        if (!holdLoad)
        {
            _loadGate.SetResult();
        }
    }

    /// <summary>Gets or sets the reason the next loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <summary>Gets or sets the reason the next writes fail with, or null when they succeed.</summary>
    internal string? WriteFailure { get; set; }

    /// <summary>Gets the number of writes that reached the source: enable, disable and resume.</summary>
    internal int WriteCount { get; private set; }

    /// <summary>Gets the source's entry for an automation.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <returns>The entry.</returns>
    internal AutomationEntry Entry(string automationId) =>
        _automations.Single(automation => string.Equals(automation.Id, automationId, StringComparison.Ordinal));

    /// <summary>Lets a held load complete.</summary>
    internal void CompleteLoad() => _loadGate.TrySetResult();

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await _loadGate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        if (LoadFailure is not null)
        {
            throw new AutomationsSourceException(LoadFailure);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AutomationEntry> Snapshot() => [.. _automations];

    /// <inheritdoc />
    public Task SetEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken)
    {
        Write();
        Replace(automationId, entry => entry with { IsEnabled = enabled });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeAsync(string automationId, CancellationToken cancellationToken)
    {
        Write();
        Replace(automationId, entry => entry with { Pause = null });
        return Task.CompletedTask;
    }

    private void Write()
    {
        if (WriteFailure is not null)
        {
            throw new AutomationsSourceException(WriteFailure);
        }

        WriteCount++;
    }

    private void Replace(string automationId, Func<AutomationEntry, AutomationEntry> change)
    {
        var index = _automations.FindIndex(automation => string.Equals(automation.Id, automationId, StringComparison.Ordinal));
        _automations[index] = change(_automations[index]);
    }
}
