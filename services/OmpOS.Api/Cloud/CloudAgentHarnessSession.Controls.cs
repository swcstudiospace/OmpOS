using OmpOS.Harness.Abstractions;

namespace OmpOS.Api.Cloud;

/// <summary>The capability interfaces of <see cref="CloudAgentHarnessSession"/>: each member forwards to the engine session once it is open, and answers "not open" before.</summary>
internal sealed partial class CloudAgentHarnessSession : IOmpModelControl, IOmpSessionOptions, IOmpSessionCommands, IOmpLiveSteering
{
    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <inheritdoc />
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpModelChoice> ModelChoices => (Inner as IOmpModelControl)?.ModelChoices ?? [];

    /// <inheritdoc />
    public string? CurrentModelId => (Inner as IOmpModelControl)?.CurrentModelId;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options => (Inner as IOmpSessionOptions)?.Options ?? [];

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands => (Inner as IOmpSessionCommands)?.Commands ?? [];

    /// <inheritdoc />
    public bool IsSupported => (Inner as IOmpLiveSteering)?.IsSupported ?? false;

    /// <inheritdoc />
    public Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken) =>
        Inner is IOmpModelControl control
            ? control.SetModelAsync(value, cancellationToken)
            : Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.NotOpen, null, "The cloud agent session is not open."));

    /// <inheritdoc />
    public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken) =>
        Inner is IOmpSessionOptions options
            ? options.SetOptionAsync(configId, value, cancellationToken)
            : Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.NotOpen, null, "The cloud agent session is not open."));

    /// <inheritdoc />
    public Task<OmpSteerResult> SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken) =>
        Inner is IOmpLiveSteering steering
            ? steering.SteerAsync(text, queue, cancellationToken)
            : Task.FromResult(new OmpSteerResult(OmpSteerOutcome.NotRunning, queue, "The cloud agent session is not open."));

    private void OnModelChanged(object? sender, EventArgs e) => ModelChanged?.Invoke(this, e);

    private void OnOptionsChanged(object? sender, EventArgs e) => OptionsChanged?.Invoke(this, e);

    private void OnCommandsChanged(object? sender, EventArgs e) => CommandsChanged?.Invoke(this, e);
}
