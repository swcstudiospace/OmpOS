using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>A scripted <see cref="IOmpSessionCommands"/>: the commands are set by the test, which raises the change the way an engine that advertises again does.</summary>
internal sealed class FakeSessionCommands : IOmpSessionCommands
{
    /// <summary>The commands omp 18.12.6 advertises first, in its order: a built-in with a hint, one without, and a skill.</summary>
    internal static readonly OmpCommand[] Typical =
    [
        new("security", "Plan, run, inspect, import, and compare OMP-native security scans", "<plan|scan|status>"),
        new("model", "Show current model selection", null),
        new("fast", "Toggle fast mode", "[on|ultra|off|status]"),
        new("skill:review", "Review the change", "arguments"),
    ];

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands { get; private set; } = [];

    /// <summary>Changes what the control reports and raises <see cref="CommandsChanged"/>, as an engine that advertises its commands does.</summary>
    /// <param name="commands">The commands the engine now advertises.</param>
    public void Report(params OmpCommand[] commands)
    {
        Commands = commands;
        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }
}
