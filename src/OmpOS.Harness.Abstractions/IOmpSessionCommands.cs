namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Implemented by an <see cref="IOmpSession"/> (or a host over one) whose engine advertises the slash commands of a running session. The engine
/// owns the commands: this surface only reads the latest list. It never runs a command; the engine runs one when a prompt that starts with its
/// name is sent.
/// </summary>
public interface IOmpSessionCommands
{
    /// <summary>Gets the commands the engine last advertised for the session, in the engine's order. Empty until the session is open and the engine has advertised them.</summary>
    IReadOnlyList<OmpCommand> Commands { get; }

    /// <summary>
    /// Occurs when the engine advertised a list that differs from the one before, or when the host started following another session. It can be
    /// raised on any thread; read <see cref="Commands"/> for the list.
    /// </summary>
    event EventHandler? CommandsChanged;
}
