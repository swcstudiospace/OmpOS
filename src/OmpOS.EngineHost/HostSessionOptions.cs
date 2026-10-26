using OmpOS.Harness.Abstractions;

namespace OmpOS.EngineHost;

/// <summary>
/// The session options of an <see cref="InProcessEngineHost"/>. A host binds a new session after a fault, so a client cannot hold a
/// session's own options; this one stays valid and forwards to whichever session the host has bound. Before a session is bound, and
/// when the bound session offers no options, the list is empty and a change is refused.
/// </summary>
internal sealed class HostSessionOptions : IOmpSessionOptions
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpSessionOptions? _bound;

    /// <inheritdoc />
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options => Bound()?.Options ?? [];

    /// <summary>Follows the options of <paramref name="session"/>, if it has any, and raises <see cref="OptionsChanged"/> because the options are now the session's. A session already followed changes nothing.</summary>
    /// <param name="session">The session the host bound.</param>
    public void Bind(IOmpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var options = session as IOmpSessionOptions;
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

    /// <summary>Stops following the bound session. The host calls it when it is disposed.</summary>
    public void Unbind()
    {
        IOmpSessionOptions? previous;
        lock (_gate)
        {
            previous = _bound;
            _bound = null;
        }

        if (previous is not null)
        {
            previous.OptionsChanged -= OnBoundChanged;
        }
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
