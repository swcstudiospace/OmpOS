using System.Runtime.CompilerServices;
using OmpOS.Presentation.Terminal;

namespace OmpOS.Web.Services;

/// <summary>
/// The <see cref="IRemoteTerminalTransport"/> of the real web head. The API has no pseudo-terminal channel and a page cannot start omp, so there
/// is nothing to attach to: the transport yields one chunk that says so (the terminal view shows it as the terminal's output) and ends.
/// </summary>
internal sealed class UnavailableTerminalTransport : IRemoteTerminalTransport
{
    /// <summary>The reason the terminal shows.</summary>
    internal const string Reason = "The omp terminal is not available in the web head. The hosted API has no terminal channel, and a browser page cannot start omp. Open this session in the desktop app.";

    /// <inheritdoc />
    public async IAsyncEnumerable<RemoteTerminalChunk> AttachAsync(string terminalId, long afterSequence, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalId);
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (afterSequence < 1)
        {
            yield return new RemoteTerminalChunk(1, Reason + "\r\n");
        }
    }
}
