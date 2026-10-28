namespace OmpOS.Harness.Abstractions;

/// <summary>
/// Implemented by an <see cref="IOmpSession"/> (or a host over one) whose engine lets a client choose the model of a running session.
/// The engine owns the model list and the current model: this surface only reads them and asks the engine to change the current one.
/// </summary>
public interface IOmpModelControl
{
    /// <summary>Gets the models the engine offers for the session, in the engine's order. Empty until the session is open or when the engine offers no model option.</summary>
    IReadOnlyList<OmpModelChoice> ModelChoices { get; }

    /// <summary>Gets the identifier of the model the engine uses for the session, or <see langword="null"/> when none is reported.</summary>
    string? CurrentModelId { get; }

    /// <summary>
    /// Gets whether the engine accepts image blocks in a prompt, as its <c>initialize</c> result says (<c>promptCapabilities.image</c>): <see langword="true"/> or <see langword="false"/> once the engine answered,
    /// <see langword="null"/> while it is not known (the session has not started) or when the control belongs to an engine that does not report it. Read it again after <see cref="ModelChanged"/>.
    /// </summary>
    bool? SupportsImages => null;

    /// <summary>
    /// Occurs when the current model or the list of models changed, whether this client switched the model or the engine reported
    /// the change itself. It can be raised on any thread; read <see cref="CurrentModelId"/> and <see cref="ModelChoices"/> for the values.
    /// </summary>
    event EventHandler? ModelChanged;

    /// <summary>
    /// Asks the engine to use <paramref name="value"/> for the session. A request the engine cannot take now (no open session, a
    /// running prompt, a value it does not offer) is refused without sending anything and reported in the result.
    /// </summary>
    /// <param name="value">One of the <see cref="OmpModelChoice.Value"/> entries of <see cref="ModelChoices"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the attempt ended and which model the session uses afterwards.</returns>
    Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken);
}
