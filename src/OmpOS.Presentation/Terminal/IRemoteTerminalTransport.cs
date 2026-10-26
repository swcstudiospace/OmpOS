namespace OmpOS.Presentation.Terminal;

/// <summary>
/// The transport a head uses to attach to a terminal that runs elsewhere. A head adapts its remote session client (for
/// example the herdr session client) to this interface, so the presentation layer never references a harness assembly.
/// </summary>
public interface IRemoteTerminalTransport
{
    /// <summary>Opens the output stream of a remote terminal, replaying every chunk after <paramref name="afterSequence"/>.</summary>
    /// <param name="terminalId">The remote terminal to attach to.</param>
    /// <param name="afterSequence">The highest sequence the caller has already received, or 0 to receive the whole stream.</param>
    /// <param name="cancellationToken">Cancels the attach.</param>
    /// <returns>
    /// The chunks in sequence order. The transport may deliver a chunk at or before <paramref name="afterSequence"/> again
    /// (at-least-once delivery); the caller drops those. A lost connection is reported by throwing <see cref="IOException"/>.
    /// </returns>
    IAsyncEnumerable<RemoteTerminalChunk> AttachAsync(string terminalId, long afterSequence, CancellationToken cancellationToken);
}
