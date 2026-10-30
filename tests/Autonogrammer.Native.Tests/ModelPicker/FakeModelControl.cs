using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Tests.ModelPicker;

/// <summary>A scripted <see cref="IOmpModelControl"/>: the models and the current model are set by the test, every switch request is recorded, and the answer can be scripted.</summary>
internal sealed class FakeModelControl : IOmpModelControl
{
    /// <summary>Gets the models the control reports.</summary>
    public IReadOnlyList<OmpModelChoice> ModelChoices { get; private set; } = [];

    /// <summary>Gets the model the control reports as current.</summary>
    public string? CurrentModelId { get; private set; }

    /// <summary>Gets or sets whether the control reports that the engine takes images; null reports nothing, as an engine that has not answered <c>initialize</c> does.</summary>
    public bool? SupportsImages { get; set; }

    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <summary>Gets the values a switch was requested for, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Gets or sets the answer to a switch request. Null accepts every request.</summary>
    public Func<string, OmpModelSwitchResult>? Answer { get; set; }

    /// <summary>Changes what the control reports and raises <see cref="ModelChanged"/>, as an engine that reports a change on its own does.</summary>
    /// <param name="choices">The models.</param>
    /// <param name="current">The current model.</param>
    public void Report(IReadOnlyList<OmpModelChoice> choices, string? current)
    {
        ModelChoices = choices;
        CurrentModelId = current;
        ModelChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken)
    {
        Requests.Add(value);
        var result = Answer?.Invoke(value) ?? new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, value, null);
        if (result.IsSwitched)
        {
            CurrentModelId = value;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.FromResult(result);
    }
}
