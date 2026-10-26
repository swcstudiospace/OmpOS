using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Agents;

/// <summary>
/// The session options of one session slot. A slot composes its engine lazily and again after the session is closed and reopened, so the
/// view cannot hold the options of one engine: this one stays valid and forwards to the engine the slot holds now. With no engine the
/// option list is empty and a change is refused.
/// </summary>
internal sealed class SlotSessionOptions : IOmpSessionOptions
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpSessionOptions? _bound;

    /// <inheritdoc />
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options => Bound()?.Options ?? [];

    /// <summary>Follows the options of the engine the slot holds now, or none, and raises <see cref="OptionsChanged"/> because the options are now the engine's.</summary>
    /// <param name="options">The options of the slot's engine host, or null when the slot has no engine.</param>
    public void Bind(IOmpSessionOptions? options)
    {
        IOmpSessionOptions? previous;
        lock (_gate)
        {
            if (ReferenceEquals(_bound, options))
            {
                return;
            }

            previous = _bound;
            _bound = options;
        }

        if (previous is not null)
        {
            previous.OptionsChanged -= OnBoundChanged;
        }

        if (options is not null)
        {
            options.OptionsChanged += OnBoundChanged;
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return Bound() is { } options
            ? options.SetOptionAsync(configId, value, cancellationToken)
            : Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.NotOpen, null, "The agent session is not open, so the setting cannot be changed."));
    }

    private IOmpSessionOptions? Bound()
    {
        lock (_gate)
        {
            return _bound;
        }
    }

    private void OnBoundChanged(object? sender, EventArgs e) => OptionsChanged?.Invoke(this, EventArgs.Empty);
}
