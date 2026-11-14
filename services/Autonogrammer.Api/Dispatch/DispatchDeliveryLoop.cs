using System.Net.WebSockets;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Sessions.Postgres;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Delivers a device's queued commands in sequence order over its socket. It wakes on the hub's signals and falls back to a 200 ms poll (D-21,
/// D-22). It marks each command delivered before its frame is written, and returns it to the queue if the write fails. It never delivers an expired or purged command (D-26).
/// </summary>
internal sealed class DispatchDeliveryLoop
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(200);
    private readonly IDispatchStore _store;
    private readonly DispatchSigner _signer;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="DispatchDeliveryLoop"/> class.</summary>
    /// <param name="store">The device store.</param>
    /// <param name="signer">The signer that signs each command at delivery.</param>
    /// <param name="time">The clock.</param>
    public DispatchDeliveryLoop(IDispatchStore store, DispatchSigner signer, TimeProvider time)
    {
        _store = store;
        _signer = signer;
        _time = time;
    }

    /// <summary>Delivers the queued commands after the sequence the device has applied, until the socket closes or the token ends.</summary>
    /// <param name="session">The device socket.</param>
    /// <param name="deliveredSeq">The sequence the device has applied; delivery resumes after it.</param>
    /// <param name="cancellationToken">Ends the loop.</param>
    /// <returns>A task that completes when the loop ends.</returns>
    public async Task RunAsync(DispatchHubSession session, long deliveredSeq, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            var position = deliveredSeq;
            while (session.IsOpen && !cancellationToken.IsCancellationRequested)
            {
                var queued = await _store.ListQueuedAfterAsync(session.Owner, session.DeviceId, position, cancellationToken).ConfigureAwait(false);
                foreach (var command in queued)
                {
                    position = command.Seq;
                    if (command.ExpiresAt <= _time.GetUtcNow() || command.BodyJson is null)
                    {
                        continue;
                    }

                    var frame = _signer.Sign(session.Owner, command, command.BodyJson, DispatchHandshake.NewNonce());
                    var transition = await _store.TryTransitionAsync(session.Owner, command.CommandId, DispatchCommandState.Queued, DispatchCommandState.Delivered, null, cancellationToken).ConfigureAwait(false);
                    if (transition.Status != DispatchTransitionStatus.Applied)
                    {
                        continue;
                    }

                    try
                    {
                        await session.SendAsync(frame, DispatchJsonContext.Default.DispatchCmdFrame, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is WebSocketException or IOException or OperationCanceledException)
                    {
                        // The frame may not have gone out, so the command is queued again. The compensation runs without the loop's token, so a
                        // cancelled session still returns the command to the queue. Another state means the row moved first and stays as it is.
                        _ = await _store.TryTransitionAsync(session.Owner, command.CommandId, DispatchCommandState.Delivered, DispatchCommandState.Queued, null, CancellationToken.None).ConfigureAwait(false);
                        throw;
                    }
                }

                await session.WaitForWorkAsync(Poll, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // The socket closed under the loop. The session ends.
        }
        catch (AccountErasingException)
        {
            // The account is being erased and its writes are refused, so delivery stops. The socket ends with its own close.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The session ended. The loop stops.
        }
    }
}
