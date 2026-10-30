using System.Text;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Terminal;

/// <summary>
/// The terminal surface of a head: whether a local terminal exists, and the output of an attached remote terminal. Mobile
/// and web heads show <see cref="LocalAvailability"/> instead of a local terminal and attach to a remote one; every head
/// renders <see cref="Output"/> the same way.
/// </summary>
/// <remarks>
/// The model accepts a chunk only when its sequence is exactly one more than <see cref="AckedSequence"/>. Chunks at or
/// before it are duplicates and are dropped. A chunk that skips ahead is a gap: it is not accepted, the attach ends in
/// <see cref="RemoteTerminalState.ConnectionLost"/>, and the next attach asks the transport to replay from
/// <see cref="AckedSequence"/>, so output is never duplicated and never skipped.
/// </remarks>
public sealed class TerminalSurfaceModel : ObservableModel
{
    private readonly IRemoteTerminalTransport? _transport;
    private readonly StringBuilder _output = new();
    private RemoteTerminalState _state = RemoteTerminalState.Detached;
    private long _ackedSequence;
    private string _outputText = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="TerminalSurfaceModel"/> class.</summary>
    /// <param name="host">The kind of head the surface runs in.</param>
    /// <param name="transport">The remote terminal transport, or null when the head has none configured.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="host"/> is not a defined <see cref="TerminalHostKind"/>.</exception>
    public TerminalSurfaceModel(TerminalHostKind host, IRemoteTerminalTransport? transport)
    {
        LocalAvailability = TerminalAvailability.ForLocal(host);
        _transport = transport;
    }

    /// <summary>Gets whether the head can run a local terminal and, when it cannot, why.</summary>
    public TerminalAvailability LocalAvailability { get; }

    /// <summary>Gets a value indicating whether a remote transport is configured, so a remote terminal can be attached.</summary>
    public bool CanAttachRemote => _transport is not null;

    /// <summary>Gets where the remote attach stands.</summary>
    public RemoteTerminalState State { get => _state; private set => SetProperty(ref _state, value); }

    /// <summary>Gets the highest chunk sequence accepted, or 0 before the first. Reattaching resumes after it.</summary>
    public long AckedSequence { get => _ackedSequence; private set => SetProperty(ref _ackedSequence, value); }

    /// <summary>Gets the accepted remote output, in order, without duplicates.</summary>
    public string Output { get => _outputText; private set => SetProperty(ref _outputText, value); }

    /// <summary>
    /// Attaches to a remote terminal and appends its output until the stream ends, breaks or skips a chunk. Calling it again
    /// after <see cref="RemoteTerminalState.ConnectionLost"/> resumes after <see cref="AckedSequence"/>.
    /// </summary>
    /// <param name="terminalId">The remote terminal to attach to.</param>
    /// <param name="cancellationToken">Stops the attach; the model returns to <see cref="RemoteTerminalState.Detached"/> and the cancellation propagates.</param>
    /// <returns>A task that completes when the stream has ended or broken.</returns>
    /// <exception cref="ArgumentException"><paramref name="terminalId"/> is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No remote transport is configured; check <see cref="CanAttachRemote"/>.</exception>
    public async Task AttachRemoteAsync(string terminalId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalId);
        IRemoteTerminalTransport transport = _transport
            ?? throw new InvalidOperationException("No remote terminal transport is configured for this head.");

        State = RemoteTerminalState.Attached;
        try
        {
            await foreach (RemoteTerminalChunk chunk in transport.AttachAsync(terminalId, AckedSequence, cancellationToken).ConfigureAwait(false))
            {
                if (chunk.Sequence <= AckedSequence)
                {
                    continue;
                }

                if (chunk.Sequence != AckedSequence + 1)
                {
                    State = RemoteTerminalState.ConnectionLost;
                    return;
                }

                _ = _output.Append(chunk.Text);
                Output = _output.ToString();
                AckedSequence = chunk.Sequence;
            }

            State = RemoteTerminalState.ConnectionLost;
        }
        catch (IOException)
        {
            State = RemoteTerminalState.ConnectionLost;
        }
        catch (OperationCanceledException)
        {
            State = RemoteTerminalState.Detached;
            throw;
        }
    }
}
