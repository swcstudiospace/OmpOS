using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Dispatch.Audit;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Applies the device's own frames to its command rows: ack (the acceptance states), res (the terminal result) and evt (progress). A state change is a
/// compare-and-set against <see cref="DispatchTransitions"/> with its feed event in the same transaction. Input outside the table changes nothing and
/// writes one audit row. The hub never completes a command on its own, and a frame can name only a command of its own device. Device text is redacted
/// of the dispatch signing keys and capped (1024 characters for evt, 4096 for res) before it reaches the feed.
/// </summary>
internal sealed class DispatchDeviceFrames(IDispatchStore store, DispatchAuditWriter audit, IOptions<DispatchOptions> options)
{
    private const int EventTextCap = 1024;
    private const int ResultTextCap = 4096;
    private const string Duplicate = "duplicate";
    private const string StateFeedKind = "command.state";
    private const string ResultFeedKind = "command.result";
    private const string EventFeedKind = "command.evt";
    private readonly CloudSecretRedactor _redactor = new(new[] { options.Value.Credentials.SigningKeyPem, options.Value.CommandSigning.KeyPem }.OfType<string>());

    /// <summary>Applies one ack. Only the acceptance states are acked: received, awaiting_approval, accepted and running. A duplicate changes nothing and writes no audit row.</summary>
    /// <param name="accountId">The owning account of the session.</param>
    /// <param name="deviceId">The device of the session.</param>
    /// <param name="payload">The UTF-8 frame.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is applied or refused.</returns>
    public async ValueTask AckAsync(string accountId, string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        var frame = TryRead(payload, DispatchJsonContext.Default.DispatchAckFrame);
        if (frame is null || string.Equals(frame.State, Duplicate, StringComparison.Ordinal))
        {
            return;
        }

        var current = await OwnedAsync(accountId, deviceId, frame.CommandId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            await audit.RecordRejectedAsync(accountId, frame.CommandId, "command_not_owned", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!DispatchCommandStateWire.TryParse(frame.State, out var desired) || !IsAcceptanceState(desired))
        {
            await audit.RecordRejectedAsync(accountId, current.CommandId, "ack_outside_table", cancellationToken).ConfigureAwait(false);
            return;
        }

        var feed = new DispatchFeedDraft(StateFeedKind, StatePayload(current.CommandId, desired, null));
        await MoveAsync(accountId, current, desired, null, feed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Applies one terminal result. Completed, failed and cancelled are the device's terminal states; the hub never writes completed itself.</summary>
    /// <param name="accountId">The owning account of the session.</param>
    /// <param name="deviceId">The device of the session.</param>
    /// <param name="payload">The UTF-8 frame.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is applied or refused.</returns>
    public async ValueTask ResultAsync(string accountId, string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        var frame = TryRead(payload, DispatchJsonContext.Default.DispatchResFrame);
        if (frame is null)
        {
            return;
        }

        var current = await OwnedAsync(accountId, deviceId, frame.CommandId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            await audit.RecordRejectedAsync(accountId, frame.CommandId, "command_not_owned", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!DispatchCommandStateWire.TryParse(frame.State, out var desired) || !IsResultState(desired))
        {
            await audit.RecordRejectedAsync(accountId, current.CommandId, "result_outside_table", cancellationToken).ConfigureAwait(false);
            return;
        }

        var reason = ResultReason(desired, frame.Reason);
        var text = Cap(_redactor.Redact(frame.Text), ResultTextCap);
        var feed = new DispatchFeedDraft(ResultFeedKind, ResultPayload(current.CommandId, desired, reason, text));
        await MoveAsync(accountId, current, desired, reason, feed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records one progress event on the feed. The command must be in an acceptance state, and the event does not change the command.</summary>
    /// <param name="accountId">The owning account of the session.</param>
    /// <param name="deviceId">The device of the session.</param>
    /// <param name="payload">The UTF-8 frame.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the frame is recorded or refused.</returns>
    public async ValueTask EventAsync(string accountId, string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        var frame = TryRead(payload, DispatchJsonContext.Default.DispatchEvtFrame);
        if (frame is null)
        {
            return;
        }

        var current = await OwnedAsync(accountId, deviceId, frame.CommandId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            await audit.RecordRejectedAsync(accountId, frame.CommandId, "command_not_owned", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!IsAcceptanceState(current.State) || !IsEventKind(frame.Kind))
        {
            await audit.RecordRejectedAsync(accountId, current.CommandId, "event_outside_table", cancellationToken).ConfigureAwait(false);
            return;
        }

        var text = Cap(_redactor.Redact(frame.Text), EventTextCap);
        _ = await store.AppendFeedEventAsync(accountId, EventFeedKind, EventPayload(current.CommandId, frame.Kind, text), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask MoveAsync(string accountId, DispatchCommandRecord current, DispatchCommandState desired, DispatchFailureReason? reason, DispatchFeedDraft feed, CancellationToken cancellationToken)
    {
        if (!DispatchTransitions.Allows(current.State, desired))
        {
            await audit.RecordRejectedAsync(accountId, current.CommandId, "transition_outside_table", cancellationToken).ConfigureAwait(false);
            return;
        }

        var transition = await store.TryTransitionAsync(accountId, current.CommandId, current.State, desired, reason, feed, cancellationToken).ConfigureAwait(false);
        if (transition.Status != DispatchTransitionStatus.Applied)
        {
            await audit.RecordRejectedAsync(accountId, current.CommandId, "state_changed", cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<DispatchCommandRecord?> OwnedAsync(string accountId, string deviceId, string commandId, CancellationToken cancellationToken)
    {
        var row = await store.GetCommandAsync(accountId, commandId, cancellationToken).ConfigureAwait(false);
        return row is not null && string.Equals(row.DeviceId, deviceId, StringComparison.Ordinal) ? row : null;
    }

    private static T? TryRead<T>(byte[] payload, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(payload, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsAcceptanceState(DispatchCommandState state) =>
        state is DispatchCommandState.Received or DispatchCommandState.AwaitingApproval or DispatchCommandState.Accepted or DispatchCommandState.Running;

    private static bool IsResultState(DispatchCommandState state) =>
        state is DispatchCommandState.Completed or DispatchCommandState.Failed or DispatchCommandState.Cancelled;

    private static bool IsEventKind(string kind) =>
        kind.Length is >= 1 and <= 32
        && char.IsAsciiLetterLower(kind[0])
        && kind.All(static character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '_' or '.');

    private static DispatchFailureReason? ResultReason(DispatchCommandState desired, string? wire)
    {
        if (desired == DispatchCommandState.Completed)
        {
            return null;
        }

        if (DispatchFailureReasonWire.TryParse(wire, out var reason) && IsDeviceReason(reason))
        {
            return reason;
        }

        return desired == DispatchCommandState.Cancelled ? DispatchFailureReason.CancelledByUser : DispatchFailureReason.ExecutionError;
    }

    private static bool IsDeviceReason(DispatchFailureReason reason) =>
        reason is DispatchFailureReason.UnsupportedVersion
            or DispatchFailureReason.UnsupportedOperation
            or DispatchFailureReason.CapabilityDisabled
            or DispatchFailureReason.ApprovalDenied
            or DispatchFailureReason.PolicyDenied
            or DispatchFailureReason.PayloadInvalid
            or DispatchFailureReason.PayloadTooLarge
            or DispatchFailureReason.DeviceBusy
            or DispatchFailureReason.ExecutionError
            or DispatchFailureReason.CancelledByUser;

    private static string? Cap(string? text, int limit)
    {
        if (text is null || text.Length <= limit)
        {
            return text;
        }

        var cut = limit;
        if (char.IsHighSurrogate(text[cut - 1]))
        {
            cut--;
        }

        return text[..cut];
    }

    private static string StatePayload(string commandId, DispatchCommandState state, DispatchFailureReason? reason) => new JsonObject
    {
        ["commandId"] = commandId,
        ["state"] = DispatchCommandStateWire.ToWire(state),
        ["reason"] = reason is { } failure ? DispatchFailureReasonWire.ToWire(failure) : null,
    }.ToJsonString();

    private static string ResultPayload(string commandId, DispatchCommandState state, DispatchFailureReason? reason, string? text) => new JsonObject
    {
        ["commandId"] = commandId,
        ["state"] = DispatchCommandStateWire.ToWire(state),
        ["reason"] = reason is { } failure ? DispatchFailureReasonWire.ToWire(failure) : null,
        ["text"] = text,
    }.ToJsonString();

    private static string EventPayload(string commandId, string kind, string? text) => new JsonObject
    {
        ["commandId"] = commandId,
        ["kind"] = kind,
        ["text"] = text,
    }.ToJsonString();
}
