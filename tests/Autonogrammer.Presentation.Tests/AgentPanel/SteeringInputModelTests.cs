using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Runs;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// Steering input over the existing run scheduler: steer versus follow-up, held messages, the queue cap and its copy, the history, and a
/// recorder that may fail without ever losing the queued message (46-UI-SPEC, Steering History Tab and Composer Behaviour).
/// </summary>
public sealed class SteeringInputModelTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly ScriptedRunInput _input = new();
    private readonly RunInputScheduler _scheduler;

    /// <summary>Creates the fixture with a scheduler whose run is already active.</summary>
    public SteeringInputModelTests()
    {
        _scheduler = new RunInputScheduler(_input);
        _scheduler.RunStarted();
    }

    /// <summary>A blank message does nothing, and a message with no active run is told to go as a prompt instead.</summary>
    [Fact]
    public async Task BlankTextIsIgnoredAndNoRunIsReported()
    {
        using var model = NewModel();

        (await model.SubmitAsync("   ", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Ignored);
        await _scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await model.SubmitAsync("hello", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.NoActiveRun);

        model.History.ShouldBeEmpty();
        _input.Steers.ShouldBeEmpty();
        model.IsRunActive.ShouldBeFalse();
    }

    /// <summary>A steer the engine takes at once is delivered, and with no recorder it is not recorded.</summary>
    [Fact]
    public async Task ASteerTheEngineTakesAtOnceIsDelivered()
    {
        using var model = NewModel();

        var outcome = await model.SubmitAsync("use the smaller model", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        _input.Steers.ShouldBe(["use the smaller model"]);
        var entry = model.History.Single();
        entry.Mode.ShouldBe(SteeringMode.Steer);
        entry.KindText.ShouldBe("Steer");
        entry.DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);
        entry.DeliveryText.ShouldBe("Delivered");
        entry.RecordStatus.ShouldBe(SteeringRecordStatus.NotRecorded);
        entry.RecordText.ShouldBe("Not recorded: Substrate is not reachable");
        entry.StatusLine.ShouldBe("Delivered \u00B7 Not recorded: Substrate is not reachable");
        entry.HeaderText.ShouldBe("Steer \u00B7 just now");
        entry.Text.ShouldBe("use the smaller model");
    }

    /// <summary>A steer the engine holds until its turn starts is reported as held, and the held count follows.</summary>
    [Fact]
    public async Task AHeldSteerIsReportedAsHeld()
    {
        _input.HoldSteers = true;
        using var model = NewModel();
        var announced = PropertyChangeRecorder.Record(model);

        await model.SubmitAsync("first", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.HeldUntilTurnEnds);
        model.History.Single().DeliveryText.ShouldBe("Held until the active turn ends");
        model.HeldCount.ShouldBe(1);
        model.HeldText.ShouldBe("1 steering message is held until the active turn ends.");
        announced.ShouldAnnounce(nameof(SteeringInputModel.HeldCount));
        announced.ShouldAnnounce(nameof(SteeringInputModel.HeldText));

        await model.SubmitAsync("second", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.HeldText.ShouldBe("2 steering messages are held until the active turn ends.");
    }

    /// <summary>A follow-up waits for the run to complete and then starts, and is reported as queued.</summary>
    [Fact]
    public async Task AFollowUpWaitsForTheRunToComplete()
    {
        using var model = NewModel();

        await model.SubmitAsync("then write the tests", SteeringMode.FollowUp, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.History.Single().Mode.ShouldBe(SteeringMode.FollowUp);
        model.History.Single().KindText.ShouldBe("Follow-up");
        model.History.Single().DeliveryText.ShouldBe("Queued after the active turn");
        model.QueuedFollowUpCount.ShouldBe(1);
        _input.FollowUps.ShouldBeEmpty();

        await _scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        _input.FollowUps.ShouldBe(["then write the tests"]);
    }

    /// <summary>A steer sent while follow-ups wait queues behind them, so the messages keep their send order.</summary>
    [Fact]
    public async Task ASteerBehindAFollowUpKeepsSendOrder()
    {
        using var model = NewModel();
        await model.SubmitAsync("follow", SteeringMode.FollowUp, TestContext.Current.CancellationToken).ConfigureAwait(true);

        await model.SubmitAsync("steer", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        _input.Steers.ShouldBeEmpty();
        model.QueuedFollowUpCount.ShouldBe(2);
        model.History[0].DeliveryStatus.ShouldBe(SteeringDeliveryStatus.QueuedAfterTurn);
    }

    /// <summary>The mode property chooses what the next message does when no mode is given.</summary>
    [Fact]
    public async Task TheModePropertyChoosesTheNextMessageKind()
    {
        using var model = NewModel();
        model.Mode.ShouldBe(SteeringMode.Steer);
        model.Mode = SteeringMode.FollowUp;

        await model.SubmitAsync("later", TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.History.Single().Mode.ShouldBe(SteeringMode.FollowUp);
        model.QueuedFollowUpCount.ShouldBe(1);
    }

    /// <summary>A recorder receives the entry with its length and hash prefix, and the entry reads as recorded.</summary>
    [Fact]
    public async Task ARecorderReceivesTheEntryAndItReadsAsRecorded()
    {
        var seen = new List<(SteeringMode Mode, int Length, string Hash)>();
        using var model = NewModel((entry, _) =>
        {
            seen.Add((entry.Mode, entry.Length, entry.TextSha256Prefix));
            return Task.CompletedTask;
        });
        const string Text = "use the smaller model";

        await model.SubmitAsync(Text, SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)))[..12];
        seen.ShouldBe([(SteeringMode.Steer, Text.Length, expectedHash)]);
        model.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.Recorded);
        model.History.Single().RecordText.ShouldBe("Recorded in Substrate");
        model.History.Single().StatusLine.ShouldBe("Delivered \u00B7 Recorded in Substrate");
        model.RecordedCount.ShouldBe(1);
    }

    /// <summary>A recorder that throws never loses the queued message: it is delivered and in the history, marked record failed.</summary>
    [Fact]
    public async Task AThrowingRecorderNeverLosesTheQueuedMessage()
    {
        _input.HoldSteers = true;
        using var model = NewModel((_, _) => throw new InvalidOperationException("substrate exploded"));

        var outcome = await model.SubmitAsync("keep me", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        _input.Steers.ShouldBe(["keep me"]);
        model.HeldCount.ShouldBe(1);
        model.History.Count.ShouldBe(1);
        model.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.RecordFailed);
        model.History.Single().RecordText.ShouldBe("Record failed");
        model.RecordedCount.ShouldBe(0);

        var next = await model.SubmitAsync("and again", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        next.ShouldBe(SteeringSubmitOutcome.Accepted);
        _input.Steers.ShouldBe(["keep me", "and again"]);
        model.History.Count.ShouldBe(2);
    }

    /// <summary>A recorder that is cancelled by the caller marks the entry record failed and keeps the message.</summary>
    [Fact]
    public async Task ACancelledRecorderKeepsTheMessage()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = NewModel(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(true);
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var submit = model.SubmitAsync("keep me", SteeringMode.Steer, cancellation.Token);
        await started.Task.ConfigureAwait(true);
        model.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.Recording);
        model.History.Single().RecordText.ShouldBe("Recording\u2026");
        await cancellation.CancelAsync().ConfigureAwait(true);
        var outcome = await submit.ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        _input.Steers.ShouldBe(["keep me"]);
        model.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.RecordFailed);
    }

    /// <summary>A recorder that never answers is given up on after the timeout, so the composer is not stuck.</summary>
    [Fact]
    public async Task ASilentRecorderTimesOut()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = NewModel(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(true);
        });

        var submit = model.SubmitAsync("keep me", SteeringMode.Steer, TestContext.Current.CancellationToken);
        await started.Task.ConfigureAwait(true);
        _clock.Advance(SteeringInputModel.RecorderTimeout - TimeSpan.FromSeconds(1));
        submit.IsCompleted.ShouldBeFalse();
        _clock.Advance(TimeSpan.FromSeconds(1));
        await submit.ConfigureAwait(true);

        model.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.RecordFailed);
        _input.Steers.ShouldBe(["keep me"]);
    }

    /// <summary>While Substrate is unreachable the recorder is not called and the entry reads not recorded.</summary>
    [Fact]
    public async Task AnUnreachableSubstrateSkipsTheRecorder()
    {
        var calls = 0;
        using var model = NewModel((_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });
        model.IsRecordingAvailable.ShouldBeTrue();
        model.IsSubstrateReachable = false;

        await model.SubmitAsync("one", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        calls.ShouldBe(0);
        model.IsRecordingAvailable.ShouldBeFalse();
        model.History.Single().RecordStatus.ShouldBe(SteeringRecordStatus.NotRecorded);

        model.IsSubstrateReachable = true;
        await model.SubmitAsync("two", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        calls.ShouldBe(1);
        model.History[0].RecordStatus.ShouldBe(SteeringRecordStatus.Recorded);
    }

    /// <summary>The 17th queued message is refused with the spec copy, the input never sees it, and no entry is added.</summary>
    [Fact]
    public async Task TheQueueCapRefusesWithTheSpecCopy()
    {
        _input.HoldSteers = true;
        using var model = NewModel();
        for (var index = 0; index < RunInputScheduler.MaxQueuedMessages; index++)
        {
            (await model.SubmitAsync($"message {index}", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);
        }

        var refused = await model.SubmitAsync("one too many", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        refused.ShouldBe(SteeringSubmitOutcome.QueueFull);
        model.RefusalText.ShouldBe("The queue holds 16 messages. Wait for the active turn to end, then send again.");
        model.History.Count.ShouldBe(16);
        _input.Steers.Count.ShouldBe(16);
        _input.Steers.ShouldNotContain("one too many", StringComparer.Ordinal);

        var refusedFollowUp = await model.SubmitAsync("also too many", SteeringMode.FollowUp, TestContext.Current.CancellationToken).ConfigureAwait(true);

        refusedFollowUp.ShouldBe(SteeringSubmitOutcome.QueueFull);
        model.QueuedFollowUpCount.ShouldBe(0);
    }

    /// <summary>An accepted message clears the refusal, and so does the end of the run.</summary>
    [Fact]
    public async Task TheRefusalClearsOnTheNextAcceptedMessageAndWhenTheRunEnds()
    {
        _input.HoldSteers = true;
        using var model = NewModel();
        _input.SteersHeld = RunInputScheduler.MaxQueuedMessages;
        await model.SubmitAsync("refused", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.RefusalText.ShouldNotBeNull();

        _input.SteersHeld = 0;
        await model.SubmitAsync("accepted", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.RefusalText.ShouldBeNull();

        _input.SteersHeld = RunInputScheduler.MaxQueuedMessages;
        await model.SubmitAsync("refused again", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.RefusalText.ShouldNotBeNull();

        await _scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.RefusalText.ShouldBeNull();
        model.IsRunActive.ShouldBeFalse();
    }

    /// <summary>The history is newest first, its text is capped, and it keeps at most 100 entries.</summary>
    [Fact]
    public async Task TheHistoryIsNewestFirstCappedInTextAndInCount()
    {
        using var model = NewModel();
        var long800 = new string('x', 800);

        await model.SubmitAsync(long800, SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var first = model.History.Single();
        first.Text.Length.ShouldBe(SteeringInputModel.MaxTextChars + 1);
        first.Text.ShouldEndWith("\u2026");
        first.Length.ShouldBe(800);
        first.TextSha256Prefix.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(long800)))[..12]);

        for (var index = 0; index < 104; index++)
        {
            await model.SubmitAsync($"message {index}", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        model.History.Count.ShouldBe(SteeringInputModel.MaxHistory);
        model.History[0].Text.ShouldBe("message 103");
        model.History[0].Id.ShouldBe("steer-105");
        model.History.ShouldNotContain(entry => entry.Length == 800);
    }

    /// <summary>Relative times of history entries follow the clock when the model refreshes them.</summary>
    [Fact]
    public async Task HistoryTimesFollowTheClock()
    {
        using var model = NewModel();
        await model.SubmitAsync("hello", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var entry = model.History.Single();
        var announced = PropertyChangeRecorder.Record(entry);

        _clock.Advance(TimeSpan.FromMinutes(5));
        model.RefreshTimes();

        entry.TimeText.ShouldBe("5 min ago");
        entry.HeaderText.ShouldBe("Steer \u00B7 5 min ago");
        announced.ShouldAnnounce(nameof(SteeringEntry.TimeText));
        announced.ShouldAnnounce(nameof(SteeringEntry.HeaderText));
    }

    /// <summary>The run-active flag follows the scheduler.</summary>
    [Fact]
    public async Task TheRunActiveFlagFollowsTheScheduler()
    {
        using var model = NewModel();
        var announced = PropertyChangeRecorder.Record(model);
        model.IsRunActive.ShouldBeTrue();

        await _scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.IsRunActive.ShouldBeFalse();
        _scheduler.RunStarted();

        announced.Count(name => string.Equals(name, nameof(SteeringInputModel.IsRunActive), StringComparison.Ordinal)).ShouldBe(2);
        model.IsRunActive.ShouldBeTrue();
    }

    /// <summary>A null text and an undefined mode are programming errors and throw; a recorder-less model reports it cannot record.</summary>
    [Fact]
    public async Task InvalidArgumentsThrow()
    {
        using var model = NewModel();

        await Should.ThrowAsync<ArgumentNullException>(() => model.SubmitAsync(null!, SteeringMode.Steer, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => model.SubmitAsync("x", (SteeringMode)9, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        model.IsRecordingAvailable.ShouldBeFalse();
    }

    /// <summary>After disposal the model no longer follows the scheduler.</summary>
    [Fact]
    public async Task DisposeStopsFollowingTheScheduler()
    {
        var model = NewModel();
        var announced = PropertyChangeRecorder.Record(model);

        model.Dispose();
        await _scheduler.RunCompletedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        announced.ShouldBeEmpty();
    }

    private SteeringInputModel NewModel(Func<SteeringEntry, CancellationToken, Task>? recorder = null) =>
        new(_scheduler, _clock, recorder);
}
