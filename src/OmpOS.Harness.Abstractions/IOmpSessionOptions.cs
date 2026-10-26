namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Implemented by an <see cref="IOmpSession"/> (or a host over one) whose engine lets a client read and change the select-style
/// options of a running session, for example its mode and its thinking level. The engine owns the options and their values: this
/// surface only reads them and asks the engine to change the current value of one.
/// </summary>
public interface IOmpSessionOptions
{
    /// <summary>Gets the options the engine reports for the session, in the engine's order. Empty until the session is open.</summary>
    IReadOnlyList<OmpSessionOption> Options { get; }

    /// <summary>
    /// Occurs when an option, its current value or its values changed, whether this client changed it or the engine reported the change
    /// itself. It can be raised on any thread; read <see cref="Options"/> for the values.
    /// </summary>
    event EventHandler? OptionsChanged;

    /// <summary>
    /// Asks the engine to give the option <paramref name="configId"/> the value <paramref name="value"/>. A request the engine cannot take now
    /// (no open session, a running prompt, an option or a value it does not offer) is refused without sending anything and reported in the result.
    /// </summary>
    /// <param name="configId">The <see cref="OmpSessionOption.Id"/> of one of <see cref="Options"/>.</param>
    /// <param name="value">One of the <see cref="OmpSessionOptionValue.Value"/> entries of that option.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the attempt ended and which value the option has afterwards.</returns>
    Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken);
}
