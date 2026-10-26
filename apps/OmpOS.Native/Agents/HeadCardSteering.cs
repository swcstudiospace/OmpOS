using System.Globalization;
using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Native.Agents;

/// <summary>
/// Steers a session from its Kanban card. The message goes into the session's own steering channel, the same one the Agent Panel composer
/// uses, and the result reports only what omp acknowledged: delivered (or queued for the end of the turn, for a follow-up), refused with
/// omp's reason, or sent but not acknowledged in time. A session that is not open, is closed or lost, or whose omp is not serving, fails
/// with a plain message and nothing about the session changes. The Substrate is told that a steering action happened, through the same
/// recorder as the composer: it carries the mode, the length and a short hash of the text, never the text, and it is never credited with
/// the delivery.
/// </summary>
internal sealed class HeadCardSteering : ICardSteering
{
    private static readonly Action<ILogger, string, Exception?> RecordFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "CardSteeringRecordFailed"),
        "The Substrate did not record a steering action from a card ({ExceptionType}); the message was still sent.");

    private readonly SessionEngineSet _host;
    private readonly ILogger? _logger;

    /// <summary>Creates the service over the engine set that owns the sessions.</summary>
    /// <param name="host">The engine set.</param>
    /// <param name="logger">Receives a note when the Substrate record fails, or null to discard it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public HeadCardSteering(SessionEngineSet host, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CardSteerResult> SteerAsync(string sessionId, string text, CardSteerMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(text);
        if (_host.SlotOf(sessionId) is not { } slot)
        {
            return CardSteerResult.Failed("This session is not open, so it cannot take a message.");
        }

        if (_host.IsClosed(sessionId))
        {
            return CardSteerResult.Failed("This session is closed. Reopen it to send it a message.");
        }

        if (_host.Registry is { } registry && registry.TryGet(sessionId, out var record) && record.Lifecycle == SessionLifecycle.Lost)
        {
            return CardSteerResult.Failed("This session was lost when the application last ended. Resume it to send it a message.");
        }

        var followUp = mode == CardSteerMode.FollowUp;
        var directive = new SteeringDirective
        {
            Mode = followUp ? SteeringDirectiveMode.FollowUp : SteeringDirectiveMode.Steer,
            Text = text,
        };
        var sentAt = _host.Now;
        SteeringReceipt receipt;
        try
        {
            receipt = await slot.Steering.SendAsync(directive, cancellationToken).ConfigureAwait(false);
        }
        catch (SteeringUnavailableException exception)
        {
            return CardSteerResult.Failed(exception.Message);
        }

        var ack = await slot.Steering.WaitForAckAsync(receipt.Id, _host.SteerAckTimeout, cancellationToken).ConfigureAwait(false);
        var result = Describe(ack, followUp);
        if (result.IsAccepted)
        {
            await RecordAsync(slot, text, followUp, sentAt, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Turns the acknowledgement into the plain message of a card: a missing acknowledgement is reported as exactly that.</summary>
    /// <param name="ack">What omp answered, or null when it did not answer in time.</param>
    /// <param name="followUp">Whether the message was a follow-up.</param>
    /// <returns>The result.</returns>
    internal CardSteerResult Describe(SteeringAck? ack, bool followUp)
    {
        if (ack is null)
        {
            return CardSteerResult.Queued("Sent; omp has not acknowledged it yet.");
        }

        var time = _host.Now.UtcDateTime.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        return ack.Status switch
        {
            SteeringAckStatus.Delivered when followUp => CardSteerResult.Queued(string.Create(CultureInfo.InvariantCulture, $"Queued for the end of the turn at {time}.")),
            SteeringAckStatus.Delivered => CardSteerResult.Delivered(string.Create(CultureInfo.InvariantCulture, $"Sent to the session at {time}.")),
            SteeringAckStatus.Rejected => CardSteerResult.Failed($"omp refused the message: {ReasonText(ack.Reason)}."),
            _ => CardSteerResult.Failed($"omp could not deliver the message: {ReasonText(ack.Reason)}."),
        };
    }

    private static string ReasonText(SteeringAckReason? reason) => reason switch
    {
        SteeringAckReason.InvalidJson or SteeringAckReason.InvalidSchema => "the message was not in a form omp could read",
        SteeringAckReason.TooLarge => "the message is too long",
        SteeringAckReason.BadId => "its identifier was not valid",
        SteeringAckReason.Duplicate => "omp had already received it",
        SteeringAckReason.BlankText => "the message is empty",
        SteeringAckReason.DeliverError => "delivering it into the session failed inside omp",
        _ => "omp gave no reason",
    };

    private async Task RecordAsync(SessionSlot slot, string text, bool followUp, DateTimeOffset at, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await slot.PlanAsync(cancellationToken).ConfigureAwait(false);
            if (plan.Steering is { } adapter)
            {
                await adapter.RecordAsync(SteeringRecorderAdapter.ToRecord(text, followUp, at), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Recording is an audit side effect: its failure never changes what the card reports about the delivery.
            if (_logger is not null)
            {
                RecordFailed(_logger, exception.GetType().Name, null);
            }
        }
    }
}
