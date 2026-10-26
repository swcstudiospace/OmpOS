using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sessions.Tests;

/// <summary>
/// Gate transcript replay (ROADMAP Phase 37, criterion 3). A client folds the events it has acknowledged, then resumes from the
/// repository after its last acknowledged sequence. The resumed state must equal a full replay, with no block delivered twice,
/// and a cancelled response must not end the stream.
/// </summary>
public sealed class ReplayFromAcknowledgedSequenceTests
{
    private const string SessionId = "s-ack";
    private const string CorrelationId = "s-ack/turn-1";
    private const string FirstBlockId = "block-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Turn one is cancelled after its first block (sequence 4 is the cancelled completion); turn two runs on the same session.</summary>
    private static readonly HarnessEvent[] Script =
    [
        new TextDeltaEvent("First "),
        new UiBlockEvent(new MarkdownBlock { Id = FirstBlockId, Markdown = "**one**" }),
        new TextDeltaEvent("cut"),
        new CompletedEvent("cancelled"),
        new TextDeltaEvent("Second "),
        new UiBlockEvent(new MarkdownBlock { Id = "block-2", Markdown = "# two" }),
        new TextDeltaEvent("done"),
        new CompletedEvent("completed"),
    ];

    /// <summary>Resuming after every acknowledged sequence, from none to all, reproduces the full replay exactly.</summary>
    [Fact]
    public async Task ResumingAfterEveryAcknowledgedSequenceYieldsTheSameStateAsAFullReplay()
    {
        var ct = TestContext.Current.CancellationToken;
        ISessionRepository repository = await StoreScriptAsync(ct).ConfigureAwait(true);

        List<StreamEvent> all = await ReadFromAsync(repository, 1, ct).ConfigureAwait(true);
        all.Count.ShouldBe(Script.Length);
        ProjectionState full = all.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);
        full.Blocks.Select(static block => block.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(full.Blocks.Count);
        full.CompletionReason.ShouldBe("completed");

        for (var acknowledged = 0; acknowledged <= all.Count; acknowledged++)
        {
            ProjectionState held = all.Take(acknowledged).Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);
            List<StreamEvent> missed = await ReadFromAsync(repository, acknowledged + 1, ct).ConfigureAwait(true);
            ProjectionState resumed = missed.Aggregate(held, HarnessEventProjection.Fold);

            Snapshot(resumed).ShouldBe(Snapshot(full), $"resume after acknowledged sequence {acknowledged}");
        }
    }

    /// <summary>
    /// The repository's replay bound is inclusive, so the resume bound must be the last acknowledged sequence plus one. Passing the
    /// acknowledged sequence itself re-delivers the block the client already holds.
    /// </summary>
    [Fact]
    public async Task ResumingAtTheAcknowledgedSequenceItselfRedeliversThatBlock()
    {
        var ct = TestContext.Current.CancellationToken;
        ISessionRepository repository = await StoreScriptAsync(ct).ConfigureAwait(true);
        List<StreamEvent> all = await ReadFromAsync(repository, 1, ct).ConfigureAwait(true);
        ProjectionState full = all.Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);

        // Sequence 2 is block-1; the client has acknowledged it.
        ProjectionState held = all.Take(2).Aggregate(ProjectionState.Empty, HarnessEventProjection.Fold);
        List<StreamEvent> redelivered = await ReadFromAsync(repository, 2, ct).ConfigureAwait(true);
        ProjectionState wrong = redelivered.Aggregate(held, HarnessEventProjection.Fold);

        wrong.Blocks.Count(block => string.Equals(block.Id, FirstBlockId, StringComparison.Ordinal)).ShouldBe(2);
        wrong.Blocks.Count.ShouldBe(full.Blocks.Count + 1);
    }

    /// <summary>After a cancelled completion the stream keeps serving the next turn, from the sequence after the cancellation.</summary>
    [Fact]
    public async Task CancelledResponseDoesNotEndTheStream()
    {
        var ct = TestContext.Current.CancellationToken;
        ISessionRepository repository = await StoreScriptAsync(ct).ConfigureAwait(true);

        List<StreamEvent> afterCancel = await ReadFromAsync(repository, 5, ct).ConfigureAwait(true);

        afterCancel.Select(static evt => evt.Seq).ShouldBe([5L, 6L, 7L, 8L]);
        afterCancel[0].Payload.ShouldBeOfType<TextDeltaPayload>().Delta.ShouldBe("Second ");
        afterCancel[^1].Payload.ShouldBeOfType<CompletedPayload>().Reason.ShouldBe("completed");
    }

    private static async Task<ISessionRepository> StoreScriptAsync(CancellationToken ct)
    {
        var repository = new InMemorySessionRepository();
        await repository.SaveAsync(NewSession(), ct).ConfigureAwait(true);
        for (var index = 0; index < Script.Length; index++)
        {
            foreach (StreamEvent evt in HarnessEventProjection.ToStreamEvents(index + 1, SessionId, CorrelationId, Stamp, Script[index]))
            {
                await repository.AppendAsync(SessionId, evt, ct).ConfigureAwait(true);
            }
        }

        return repository;
    }

    private static async Task<List<StreamEvent>> ReadFromAsync(ISessionRepository repository, long fromSeq, CancellationToken ct)
    {
        var events = new List<StreamEvent>();
        await foreach (StreamEvent evt in repository.ListFromSeqAsync(SessionId, fromSeq, ct).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        return events;
    }

    private static Session NewSession() => new()
    {
        Id = SessionId,
        OwnerId = "dev",
        CreatedAt = Stamp,
        UpdatedAt = Stamp,
        Status = SessionStatus.Idle,
        SandboxId = null,
        Messages = [],
        Events = [],
    };

    /// <summary>Serializes the UI state to a comparable value.</summary>
    private static string Snapshot(ProjectionState state)
    {
        var blocks = JsonSerializer.Serialize(state.Blocks.ToArray(), OmpOsJsonContext.Default.UiBlockArray);
        return $"{blocks}|{state.StreamingText}|{state.CompletionReason ?? "<null>"}";
    }
}
