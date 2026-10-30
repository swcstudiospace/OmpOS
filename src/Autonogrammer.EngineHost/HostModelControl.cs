using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.EngineHost;

/// <summary>
/// The model control of an <see cref="InProcessEngineHost"/>. A host binds a new session after a fault, so a client cannot hold a
/// session's own control; this one stays valid and forwards to whichever session the host has bound. Before a session is bound, and
/// when the bound session offers no model control, the model list is empty and a switch is refused.
/// </summary>
internal sealed class HostModelControl : IOmpModelControl
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpModelControl? _bound;

    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpModelChoice> ModelChoices => Bound()?.ModelChoices ?? [];

    /// <inheritdoc />
    public string? CurrentModelId => Bound()?.CurrentModelId;

    /// <inheritdoc />
    public bool? SupportsImages => Bound()?.SupportsImages;

    /// <summary>Follows the model control of <paramref name="session"/>, if it has one, and raises <see cref="ModelChanged"/> because the model list is now the session's. A session already followed changes nothing.</summary>
    /// <param name="session">The session the host bound.</param>
    public void Bind(IOmpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var control = session as IOmpModelControl;
        IOmpModelControl? previous;
        lock (_gate)
        {
            if (ReferenceEquals(_bound, control))
            {
                return;
            }

            previous = _bound;
            _bound = control;
        }

        if (previous is not null)
        {
            previous.ModelChanged -= OnBoundChanged;
        }

        if (control is not null)
        {
            control.ModelChanged += OnBoundChanged;
        }

        ModelChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops following the bound session. The host calls it when it is disposed.</summary>
    public void Unbind()
    {
        IOmpModelControl? previous;
        lock (_gate)
        {
            previous = _bound;
            _bound = null;
        }

        if (previous is not null)
        {
            previous.ModelChanged -= OnBoundChanged;
        }
    }

    /// <inheritdoc />
    public Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return Bound() is { } control
            ? control.SetModelAsync(value, cancellationToken)
            : Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.NotOpen, null, "The agent session is not open, so the model cannot be changed."));
    }

    private IOmpModelControl? Bound()
    {
        lock (_gate)
        {
            return _bound;
        }
    }

    private void OnBoundChanged(object? sender, EventArgs e) => ModelChanged?.Invoke(this, EventArgs.Empty);
}
