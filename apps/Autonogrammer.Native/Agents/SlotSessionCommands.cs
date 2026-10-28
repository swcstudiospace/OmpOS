using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// The slash commands of one session slot. A slot composes its engine lazily and again after the session is closed and reopened, so the view
/// cannot hold the commands of one engine: this one stays valid and forwards to the engine the slot holds now. With no engine the list is empty.
/// </summary>
internal sealed class SlotSessionCommands : IOmpSessionCommands
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpSessionCommands? _bound;

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands => Bound()?.Commands ?? [];

    /// <summary>Follows the commands of the engine the slot holds now, or none, and raises <see cref="CommandsChanged"/> because the commands are now the engine's.</summary>
    /// <param name="commands">The commands of the slot's engine host, or null when the slot has no engine.</param>
    public void Bind(IOmpSessionCommands? commands)
    {
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

    private IOmpSessionCommands? Bound()
    {
        lock (_gate)
        {
            return _bound;
        }
    }

    private void OnBoundChanged(object? sender, EventArgs e) => CommandsChanged?.Invoke(this, EventArgs.Empty);
}
