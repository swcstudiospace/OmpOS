using System.Globalization;
using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// Records a steering action as a ledger <c>note</c> event with <c>payload.action</c> <c>steer</c>. The operator text is never
/// sent: the event carries only its length and the SHA-256 prefix the caller computed. The recorder never throws; an unavailable
/// server is <see cref="SteeringRecordOutcome.Skipped"/> and any other problem is <see cref="SteeringRecordOutcome.Failed"/>.
/// </summary>
public sealed partial class SubstrateSteeringRecorder : ISteeringRecorder
{
    private static readonly Action<ILogger, SteeringRecordOutcome, Exception?> RecordFailed =
        LoggerMessage.Define<SteeringRecordOutcome>(
            LogLevel.Debug,
            new EventId(1, "SubstrateSteeringNotRecorded"),
            "The steering action was not recorded in the Substrate ledger: {Outcome}.");

    private readonly SubstrateGateway _gateway;
    private readonly ILogger _logger;

    /// <summary>Creates a recorder over a gateway.</summary>
    /// <param name="gateway">The Substrate gateway.</param>
    /// <param name="logger">Receives the outcome of an unsuccessful recording. Null discards it.</param>
    public SubstrateSteeringRecorder(SubstrateGateway gateway, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        _gateway = gateway;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<SteeringRecordResult> RecordAsync(StageObservationScope scope, SteeringRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(record);
        var invalid = Validate(record);
        if (invalid is not null)
        {
            return Settle(SteeringRecordOutcome.Failed, invalid);
        }

        var emit = new SubstrateEventEmit
        {
            Kind = "note",
            Summary = "operator steering",
            SessionId = scope.AcpSessionId ?? scope.SessionId,
            GraphId = scope.GraphId,
            Surface = "omp",
            Actor = "human",
            Payload = new SubstrateEventPayload
            {
                Action = "steer",
                Item = record.TextSha256Prefix,
                Outcome = "queued",
                Mode = record.Mode,
                Length = record.Length,
            },
        };

        try
        {
            var result = await _gateway.EmitEventAsync(emit, cancellationToken).ConfigureAwait(false);
            return result.Stored
                ? new SteeringRecordResult(SteeringRecordOutcome.Recorded, null)
                : Settle(SteeringRecordOutcome.Failed, "The Substrate ledger did not store the steering event.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Settle(SteeringRecordOutcome.Failed, "Recording the steering action was cancelled.");
        }
        catch (SubstrateUnavailableException ex) when (ex.IsReachabilityFailure)
        {
            return Settle(SteeringRecordOutcome.Skipped, string.Create(CultureInfo.InvariantCulture, $"The Substrate is not available ({ex.Reason}); the steering action was not recorded."));
        }
        catch (SubstrateUnavailableException ex)
        {
            return Settle(SteeringRecordOutcome.Failed, string.Create(CultureInfo.InvariantCulture, $"The Substrate refused the steering event ({ex.Reason})."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Settle(SteeringRecordOutcome.Failed, "Recording the steering action failed.");
        }
    }

    private static string? Validate(SteeringRecord record)
    {
        if (record.Mode is not ("steer" or "follow-up"))
        {
            return "The steering mode must be steer or follow-up.";
        }

        if (record.Length < 0)
        {
            return "The steering length must not be negative.";
        }

        return string.IsNullOrEmpty(record.TextSha256Prefix) || !HashPrefix().IsMatch(record.TextSha256Prefix)
            ? "The steering hash prefix must be 12 lowercase hexadecimal characters."
            : null;
    }

    [GeneratedRegex(@"^[0-9a-f]{12}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HashPrefix();

    private SteeringRecordResult Settle(SteeringRecordOutcome outcome, string reason)
    {
        RecordFailed(_logger, outcome, null);
        return new SteeringRecordResult(outcome, reason);
    }
}
