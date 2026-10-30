using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.EngineHost;

/// <summary>
/// The slash commands of an <see cref="InProcessEngineHost"/>. A host binds a new session after a fault, so a client cannot hold a session's
/// own commands; this one stays valid and forwards to whichever session the host has bound. Before a session is bound, and when the bound
/// session advertises no commands, the list is empty.
/// </summary>
internal sealed class HostSessionCommands : IOmpSessionCommands
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpSessionCommands? _bound;

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands => Bound()?.Commands ?? [];

    /// <summary>Follows the commands of <paramref name="session"/>, if it advertises any, and raises <see cref="CommandsChanged"/> because the commands are now the session's. A session already followed changes nothing.</summary>
    /// <param name="session">The session the host bound.</param>
    public void Bind(IOmpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var commands = session as IOmpSessionCommands;
        IOmpSessionCommands? previous;
        lock (_gate)
        {
            if (ReferenceEquals(_bound, commands))
            {
                return;
            }

            previous = _bound;
            _bound = commands;
        }

        if (previous is not null)
        {
            previous.CommandsChanged -= OnBoundChanged;
        }

        if (commands is not null)
        {
            commands.CommandsChanged += OnBoundChanged;
        }

        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops following the bound session. The host calls it when it is disposed.</summary>
    public void Unbind()
    {
        IOmpSessionCommands? previous;
        lock (_gate)
        {
            previous = _bound;
            _bound = null;
        }

        if (previous is not null)
        {
            previous.CommandsChanged -= OnBoundChanged;
        }
    }

    private IOmpSessionCommands? Bound()
    {
        lock (_gate)
        {
            return _bound;
        }
    }

    private void OnBoundChanged(object? sender, EventArgs e) => CommandsChanged?.Invoke(this, EventArgs.Empty);
}
