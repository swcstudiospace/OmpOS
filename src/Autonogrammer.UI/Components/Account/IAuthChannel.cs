namespace Autonogrammer.UI.Components.Account;

/// <summary>
/// The same-origin <c>autonogrammer-auth</c> channel. The boot script is the browser adapter; tests use a fake. A channel that is not
/// same-origin is refused and never carries a code.
/// </summary>
public interface IAuthChannel
{
    /// <summary>Gets whether this channel is confined to the page origin. A false value refuses every post.</summary>
    bool SameOrigin { get; }

    /// <summary>Posts one envelope. A cross-origin channel does nothing.</summary>
    /// <param name="envelope">The envelope. A code envelope carries the callback code; an acknowledgement carries none.</param>
    void Post(AuthChannelEnvelope envelope);

    /// <summary>Subscribes to envelopes other tabs post. The handler may run on any thread.</summary>
    /// <param name="handler">The handler. It is not retained after <see cref="IDisposable.Dispose"/> of the returned subscription.</param>
    /// <returns>A subscription that removes the handler.</returns>
    IDisposable Subscribe(Action<AuthChannelEnvelope> handler);
}
