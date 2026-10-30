using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Tests.OptionChips;

/// <summary>A scripted <see cref="IOmpSessionOptions"/>: the options are set by the test, every change request is recorded, and the answer can be scripted.</summary>
internal sealed class FakeSessionOptions : IOmpSessionOptions
{
    /// <summary>The description omp gives the plan mode.</summary>
    internal const string PlanDescription = "Read-only planning mode that drafts a plan to a markdown file before any code changes";

    /// <inheritdoc />
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options { get; private set; } = [];

    /// <summary>Gets the changes requested, as option id and value, in order.</summary>
    public List<(string ConfigId, string Value)> Requests { get; } = [];

    /// <summary>Gets or sets the answer to a change request. Null accepts every request.</summary>
    public Func<string, string, OmpOptionSetResult>? Answer { get; set; }

    /// <summary>Builds the mode option as omp reports it.</summary>
    /// <param name="current">The current mode.</param>
    /// <returns>The option.</returns>
    public static OmpSessionOption Mode(string current = "default") => new(
        "mode",
        "Mode",
        "mode",
        current,
        [new OmpSessionOptionValue("default", "Default", null), new OmpSessionOptionValue("plan", "Plan", PlanDescription)]);

    /// <summary>Builds the thinking option as omp reports it.</summary>
    /// <param name="current">The current level.</param>
    /// <returns>The option.</returns>
    public static OmpSessionOption Thinking(string current = "off") => new(
        "thinking",
        "Thinking",
        "thought_level",
        current,
        [new OmpSessionOptionValue("off", "Off", null), new OmpSessionOptionValue("auto", "Auto", "Auto-detect per prompt"), new OmpSessionOptionValue("high", "high", null)]);

    /// <summary>Changes what the control reports and raises <see cref="OptionsChanged"/>, as an engine that reports a change on its own does.</summary>
    /// <param name="options">The options.</param>
    public void Report(params OmpSessionOption[] options)
    {
        Options = options;
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
    {
        Requests.Add((configId, value));
        var result = Answer?.Invoke(configId, value) ?? new OmpOptionSetResult(OmpOptionSetOutcome.Set, value, null);
        if (result.IsSet)
        {
            Options = [.. Options.Select(option => string.Equals(option.Id, configId, StringComparison.Ordinal) ? option with { CurrentValue = value } : option)];
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.FromResult(result);
    }
}
