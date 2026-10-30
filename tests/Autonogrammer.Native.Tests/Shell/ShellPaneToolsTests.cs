using System.Text.Json;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Checkpoints;
using Autonogrammer.Native.Tests.Worktrees;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Blocks;
using Autonogrammer.Native.Views.Checkpoints;
using Autonogrammer.Native.Views.Queue;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Queue;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.State;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>The tools and panels the shell mounts around a session: the worktrees tool, the checkpoints timeline, the queued messages and the hook notices of the transcript.</summary>
public sealed class ShellPaneToolsTests
{
    private const string SessionId = "session-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The Worktrees toggle and the model's open flag are one switch: the toggle opens the tool, and closing the model unchecks the toggle.</summary>
    [Fact]
    public async Task TheWorktreesToggleAndTheModelAreOneSwitch()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices { Worktrees = new ViewWorktreeBackend() });
            var toggle = main.FindControl<ToggleButton>("WorktreesToggle")!;
            var model = main.Worktrees.ShouldNotBeNull();
            toggle.IsVisible.ShouldBeTrue();
            model.IsOpen.ShouldBeFalse();

            toggle.IsChecked = true;
            ShellHarness.Settle(fixture.ServicesWindow!);
            model.IsOpen.ShouldBeTrue();
            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<TextBlock>("ToolTitle")!.Text.ShouldBe("Worktrees");

            model.Close();
            ShellHarness.Settle(fixture.ServicesWindow!);
            toggle.IsChecked.ShouldBe(false);
            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeFalse();

            model.Open();
            toggle.IsChecked.ShouldBe(true, "a slash command that opens the model shows the panel");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The Checkpoints toggle shows the timeline of the selected session and hides it again; the timeline is mounted above the composer.</summary>
    [Fact]
    public async Task TheCheckpointsToggleShowsTheTimeline()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices { Checkpoints = new ScriptedCheckpointBackend() });
            var toggle = main.FindControl<ToggleButton>("CheckpointsToggle")!;
            var view = (CheckpointsTimelineView)main.FindControl<ContentControl>("CheckpointsHost")!.Content!;
            toggle.IsVisible.ShouldBeTrue();
            view.IsVisible.ShouldBeFalse("the timeline hides itself until it is opened");

            toggle.IsChecked = true;
            ShellHarness.Settle(fixture.ServicesWindow!);
            view.IsVisible.ShouldBeTrue();

            toggle.IsChecked = false;
            ShellHarness.Settle(fixture.ServicesWindow!);
            view.IsVisible.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Tab in the prompt queues the draft while a run is active, the queued message shows under the composer, and the draft clears.</summary>
    [Fact]
    public async Task TabQueuesTheDraftBehindTheActiveRun()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var runtime = fixture.Runtime(SessionId);
            var queueView = (QueuedMessagesView)main.FindControl<ContentControl>("QueueHost")!.Content!;
            var queue = (QueuedMessagesModel)queueView.DataContext!;
            var composer = (ComposerView)main.FindControl<ContentControl>("ComposerHost")!.Content!;
            queueView.IsVisible.ShouldBeFalse("an empty queue hides itself");

            runtime.Runs.RunStarted();
            runtime.Composer.Draft = "then run the tests";
            ShellHarness.Settle(fixture.ServicesWindow!);
            composer.FindControl<TextBox>("PromptInput")!.Focus();
            fixture.ServicesWindow!.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            ShellHarness.Settle(fixture.ServicesWindow!);

            queue.Messages.Select(static message => message.Text).ShouldBe(["then run the tests"]);
            runtime.Composer.Draft.ShouldBe(string.Empty);
            queueView.IsVisible.ShouldBeTrue();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Tab does nothing special while no run is active: the draft stays and nothing is queued.</summary>
    [Fact]
    public async Task TabQueuesNothingWhenNoRunIsActive()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var runtime = fixture.Runtime(SessionId);
            var queue = (QueuedMessagesModel)((QueuedMessagesView)main.FindControl<ContentControl>("QueueHost")!.Content!).DataContext!;
            var composer = (ComposerView)main.FindControl<ContentControl>("ComposerHost")!.Content!;

            runtime.Composer.Draft = "just a draft";
            composer.FindControl<TextBox>("PromptInput")!.Focus();
            fixture.ServicesWindow!.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            ShellHarness.Settle(fixture.ServicesWindow!);

            queue.IsEmpty.ShouldBeTrue();
            runtime.Composer.Draft.ShouldBe("just a draft");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A hook notice that names a tool call hangs under that call's row, keeps its place when the row is rebuilt, and a notice with no known call sits under the conversation.</summary>
    [Fact]
    public async Task HookNoticesHangUnderTheirToolRow()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var session = fixture.Runtime(SessionId).Session;
            var transcript = (SessionView)main.FindControl<ContentControl>("TranscriptHost")!.Content!;
            Apply(session, 1, "tool_call", """{"callId":"call-1","name":"read_file"}""");
            ShellHarness.Settle(fixture.ServicesWindow!);

            transcript.AddHookNotice(HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Denied, "lint", "call-1", "Blocked by the lint hook.")));
            transcript.AddHookNotice(HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Context, "welcome", null, "Added context from the welcome hook.")));
            transcript.AddHookNotice(HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Denied, "other", "call-gone", "Blocked.")));
            ShellHarness.Settle(fixture.ServicesWindow!);

            var blocks = transcript.FindControl<StackPanel>("BlockHost")!;
            blocks.Children.OfType<ToolCallBlockView>().Single().Notices.Children.Count.ShouldBe(1);
            transcript.FindControl<StackPanel>("HookNoticeHost")!.Children.Count.ShouldBe(2, "a notice without a call and one for a call that is not shown");

            Apply(session, 2, "tool_call_update", """{"callId":"call-1","status":"completed"}""");
            ShellHarness.Settle(fixture.ServicesWindow!);

            blocks.Children.OfType<ToolCallBlockView>().Single().Notices.Children.Count.ShouldBe(1, "the notice moved to the rebuilt row");
            transcript.HookNotices.Count.ShouldBe(3);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The Review button of a consent notice asks for the Hooks page of Settings.</summary>
    [Fact]
    public async Task ReviewOnAConsentNoticeOpensTheHooksPage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var seen = new List<SettingsSection?>();
            main.SettingsRequested += (_, e) => seen.Add(e.Section);
            var transcript = (SessionView)main.FindControl<ContentControl>("TranscriptHost")!.Content!;
            transcript.AddHookNotice(HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.ConsentRequired, "lint", null, "This hook needs your approval before it runs.")));
            ShellHarness.Settle(fixture.ServicesWindow!);

            ShellHarness.RaiseClick(transcript.HookNotices.Single().Review);

            seen.ShouldBe([SettingsSection.Hooks]);
        }, Token).ConfigureAwait(true);
    }

    private static void Apply(SessionViewModel session, long sequence, string kind, string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        session.ApplyEvent(new StreamEvent
        {
            Seq = sequence,
            SessionId = SessionId,
            CorrelationId = SessionId + "/turn-1",
            Timestamp = Stamp,
            Payload = new UnknownPayload { OriginalKind = kind, Raw = document.RootElement.Clone() },
        });
    }
}
