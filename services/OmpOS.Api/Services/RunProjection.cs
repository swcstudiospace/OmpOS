using System.Text.Json;
using OmpOS.Contracts.Runs;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;

namespace OmpOS.Api.Services;

/// <summary>
/// Reads a hosted run out of its session. A run is one turn of its session, so its status, transcript and file changes are all derived
/// from the stored messages, events and session status; nothing about the output is kept separately, and any replica that shares the
/// session store reads the same run.
/// </summary>
internal static class RunProjection
{
    // Mirrors the constants the session service and the run activities write; the log carries these kinds as unknown payloads.
    private const string PromptAcceptedKind = "prompt_accepted";
    private const string ToolCallUpdateKind = "tool_call_update";
    private const string CancelledReason = "cancelled";
    private const string UserRole = "user";
    private const string FailedToolStatus = "failed";

    /// <summary>Gets the 1-based turn the session is on: the count of user prompts admitted.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The number of admitted user prompts.</returns>
    public static int TurnOf(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var turn = 0;
        foreach (var message in session.Messages)
        {
            if (string.Equals(message.Role, UserRole, StringComparison.Ordinal))
            {
                turn++;
            }
        }

        return turn;
    }

    /// <summary>Projects the run's current status and timestamps.</summary>
    /// <param name="session">The session the run executes in, as currently stored.</param>
    /// <param name="record">The run's record.</param>
    /// <returns>The run.</returns>
    public static Run ToRun(Session session, RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(record);
        var slice = SliceOf(session, record.Turn);
        var status = StatusOf(session, record, slice);
        return new Run
        {
            Id = record.Id,
            SessionId = record.SessionId,
            Status = status,
            CreatedAt = record.CreatedAt,
            CompletedAt = IsSettled(status) && slice is { Events.Count: > 0 } ? slice.Events[^1].Timestamp : null,
        };
    }

    /// <summary>
    /// Projects the run's output. A queued or running run yields its status and nothing else. A settled run yields its transcript and the
    /// file changes its harness reported.
    /// </summary>
    /// <param name="session">The session the run executes in, as currently stored.</param>
    /// <param name="record">The run's record.</param>
    /// <returns>The run output.</returns>
    public static RunOutput ToOutput(Session session, RunRecord record)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(record);
        var slice = SliceOf(session, record.Turn);
        var status = StatusOf(session, record, slice);
        if (!IsSettled(status))
        {
            return new RunOutput { RunId = record.Id, Status = status };
        }

        var turn = slice ?? new TurnSlice([], []);
        return new RunOutput
        {
            RunId = record.Id,
            Status = status,
            Transcript = new RunTranscript { Messages = turn.Messages, Events = turn.Events },
            Changes = ChangesOf(turn.Events),
        };
    }

    /// <summary>Gets whether a status is terminal.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True for completed, failed and cancelled.</returns>
    public static bool IsSettled(RunStatus status) => status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled;

    /// <summary>
    /// Derives the run's status. While the run's turn is the session's latest, the session status is authoritative; a settled status is
    /// cached on the record so a later turn cannot change it. For an older turn without a cached status, the turn's own events decide.
    /// </summary>
    private static RunStatus StatusOf(Session session, RunRecord record, TurnSlice? slice)
    {
        if (record.SettledStatus is { } cached)
        {
            return cached;
        }

        var turns = CountTurns(session);
        if (slice is null || record.Turn >= turns)
        {
            var current = session.Status switch
            {
                SessionStatus.Completed => RunStatus.Completed,
                SessionStatus.Failed => RunStatus.Failed,
                SessionStatus.Cancelled => RunStatus.Cancelled,
                SessionStatus.Running or SessionStatus.AwaitingToolApproval => record.Started ? RunStatus.Running : RunStatus.Queued,
                _ => RunStatus.Queued,
            };
            if (IsSettled(current))
            {
                record.MarkSettled(current);
            }

            return current;
        }

        var settled = SettledFromEvents(slice.Events);
        record.MarkSettled(settled);
        return settled;
    }

    /// <summary>Reads how an older turn ended from its terminal event: a completed event with the cancelled reason is a cancel, an error event a failure.</summary>
    private static RunStatus SettledFromEvents(IReadOnlyList<StreamEvent> events)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            switch (events[i].Payload)
            {
                case CompletedPayload completed:
                    return string.Equals(completed.Reason, CancelledReason, StringComparison.Ordinal) ? RunStatus.Cancelled : RunStatus.Completed;
                case ErrorPayload:
                    return RunStatus.Failed;
            }
        }

        return RunStatus.Completed;
    }

    private static int CountTurns(Session session)
    {
        var turns = 0;
        foreach (var evt in session.Events)
        {
            if (evt.Payload is UnknownPayload { OriginalKind: PromptAcceptedKind })
            {
                turns++;
            }
        }

        return turns;
    }

    /// <summary>Cuts the messages and events of one turn out of the session: from its acceptance event up to the next turn's.</summary>
    private static TurnSlice? SliceOf(Session session, int turn)
    {
        var eventStart = -1;
        var eventEnd = session.Events.Count;
        var accepted = 0;
        for (var i = 0; i < session.Events.Count; i++)
        {
            if (session.Events[i].Payload is not UnknownPayload { OriginalKind: PromptAcceptedKind })
            {
                continue;
            }

            accepted++;
            if (accepted == turn)
            {
                eventStart = i;
            }
            else if (accepted == turn + 1)
            {
                eventEnd = i;
                break;
            }
        }

        if (eventStart < 0)
        {
            return null;
        }

        var messageStart = -1;
        var messageEnd = session.Messages.Count;
        var prompts = 0;
        for (var i = 0; i < session.Messages.Count; i++)
        {
            if (!string.Equals(session.Messages[i].Role, UserRole, StringComparison.Ordinal))
            {
                continue;
            }

            prompts++;
            if (prompts == turn)
            {
                messageStart = i;
            }
            else if (prompts == turn + 1)
            {
                messageEnd = i;
                break;
            }
        }

        Message[] messages = messageStart < 0 ? [] : [.. session.Messages.Skip(messageStart).Take(messageEnd - messageStart)];
        return new TurnSlice(messages, session.Events.Skip(eventStart).Take(eventEnd - eventStart).ToArray());
    }

    /// <summary>
    /// Collects the file edits the harness reported during the turn. An edit arrives either as a file-edit block or inside a tool-call update;
    /// a later update of the same call replaces the earlier report of the same edit, so a call that reports its diff twice yields it once.
    /// An update that reports a failed call contributes nothing.
    /// </summary>
    private static List<RunFileChange> ChangesOf(IReadOnlyList<StreamEvent> events)
    {
        var changes = new List<RunFileChange>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var evt in events)
        {
            switch (evt.Payload)
            {
                case UiBlockPayload { Block: FileEditBlock block }:
                    Put(changes, positions, block.Id, new RunFileChange { Path = block.Path, OldText = block.OldText, NewText = block.NewText });
                    break;
                case UnknownPayload { OriginalKind: ToolCallUpdateKind } update:
                    PutUpdate(changes, positions, update.Raw);
                    break;
            }
        }

        return changes;
    }

    private static void PutUpdate(List<RunFileChange> changes, Dictionary<string, int> positions, JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object
            || StringOf(raw, "callId") is not { } callId
            || StringOf(raw, "payloadJson") is not { } payloadJson
            || string.Equals(StringOf(raw, "status"), FailedToolStatus, StringComparison.Ordinal))
        {
            return;
        }

        var edits = ToolUpdateContent.Read(payloadJson).Edits;
        for (var i = 0; i < edits.Count; i++)
        {
            // The key is the id the session projection gives the same edit's file-edit block.
            Put(changes, positions, $"{callId}-edit-{i}", new RunFileChange { Path = edits[i].Path, OldText = edits[i].OldText, NewText = edits[i].NewText });
        }
    }

    private static void Put(List<RunFileChange> changes, Dictionary<string, int> positions, string key, RunFileChange change)
    {
        if (positions.TryGetValue(key, out var index))
        {
            changes[index] = change;
            return;
        }

        positions[key] = changes.Count;
        changes.Add(change);
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The messages and events of one turn.</summary>
    /// <param name="Messages">The turn's messages.</param>
    /// <param name="Events">The turn's events.</param>
    private sealed record TurnSlice(IReadOnlyList<Message> Messages, IReadOnlyList<StreamEvent> Events);
}
