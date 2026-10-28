using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Agents;

/// <summary>
/// The model control of one session slot. A slot composes its engine lazily and again after the session is closed and reopened, so the
/// view cannot hold the control of one engine: this one stays valid and forwards to the engine the slot holds now. With no engine the
/// model list is empty and a switch is refused.
/// </summary>
internal sealed class SlotModelControl : IOmpModelControl
{
    private readonly System.Threading.Lock _gate = new();
    private readonly Func<CancellationToken, Task> _connect;
    private IOmpModelControl? _bound;

    /// <summary>Creates the control of a slot.</summary>
    /// <param name="connect">Starts the slot's engine session, composing the engine first when needed, so that the engine reports its models.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connect"/> is null.</exception>
    public SlotModelControl(Func<CancellationToken, Task> connect)
    {
        ArgumentNullException.ThrowIfNull(connect);

        _connect = connect;
    }

    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpModelChoice> ModelChoices => Bound()?.ModelChoices ?? [];

    /// <inheritdoc />
    public string? CurrentModelId => Bound()?.CurrentModelId;

    /// <inheritdoc />
    public bool? SupportsImages => Bound()?.SupportsImages;

    /// <summary>Follows the model control of the engine the slot holds now, or none, and raises <see cref="ModelChanged"/> because the model list is now the engine's.</summary>
    /// <param name="control">The control of the slot's engine host, or null when the slot has no engine.</param>
    public void Bind(IOmpModelControl? control)
    {
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

    /// <summary>Starts the slot's engine session so that its models are known. Does nothing when the session is already open.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes when the session is open.</returns>
    public Task ConnectAsync(CancellationToken cancellationToken) => _connect(cancellationToken);

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
