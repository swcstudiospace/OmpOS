using Autonogrammer.Contracts.Ui;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Native.Tests.Mentions;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Tests.SlashPalette;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Presentation.Modes;
using Autonogrammer.Presentation.SideChats;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// What a head hands to <see cref="AgentSessionRuntimeOptions"/> reaches the turn: the mentions travel with the prompt as resources, an active custom mode's preamble is put in front of
/// the prompt, a fork opens a new session in the rail, and the live engine signals (context usage, hook notices, an interrupted turn) are raised on the runtime.
/// </summary>
public sealed class AgentSessionRuntimeWiringTests
{
    private static readonly ScriptedTurn Reply = new([new TextDeltaEvent("a reply"), new CompletedEvent("end_turn")]);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A mention attached to the draft is sent with the prompt as a resource: its location, type, the content read at send time and its size.</summary>
    [Fact]
    public async Task AnAttachedMentionTravelsWithThePromptAsAResource()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new ScriptedPromptChannel(Reply);
            var fixture = ShellFixture.CreateWithSession(new SessionScript
            {
                Prompts = channel,
                MentionSources = static () => [new StaticMentionSource(MentionKind.File, "src/App.cs")],
            });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var composer = fixture.Runtime("session-1").Composer;
            var mentions = composer.Mentions.ShouldNotBeNull();
            composer.ContextRing.ShouldNotBeNull();

            composer.Draft = "see @App";
            composer.CaretIndex = composer.Draft.Length;
            await mentions.Picker.WaitSettledAsync().ConfigureAwait(true);
            await composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);
            composer.Draft.ShouldBe("see @src/App.cs ");
            channel.Prompts.ShouldBeEmpty("Enter attached the mention instead of sending");
            await composer.SendAsync().ConfigureAwait(true);
            await ShellWait.UntilAsync(() => channel.Options.Count == 1, "the prompt to be submitted", Token).ConfigureAwait(true);

            var resource = channel.Options.Single().Resources.ShouldHaveSingleItem();
            resource.Location.ShouldBe(new Uri("file:///w/src/App.cs"));
            resource.MimeType.ShouldBe("text/plain");
            resource.Text.ShouldBe("body");
            resource.DisplayName.ShouldBe("src/App.cs");
            resource.OriginalLength.ShouldBe(4);
            resource.Truncated.ShouldBeFalse();
            channel.Prompts.ShouldHaveSingleItem().ShouldStartWith("see @src/App.cs", Case.Sensitive);
            mentions.Chips.ShouldBeEmpty("an accepted send clears the chips");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A session composed without mention sources offers no picker and no context ring.</summary>
    [Fact]
    public async Task ASessionWithoutMentionSourcesHasNoMentions()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(Reply) });
            await using var fixtureScope = fixture.ConfigureAwait(true);

            var composer = fixture.Runtime("session-1").Composer;

            composer.Mentions.ShouldBeNull();
            composer.ContextRing.ShouldBeNull();
            composer.MentionSender.ShouldBeNull();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>An active custom mode puts its preamble in front of the prompt; the transcript keeps what the user typed; a slash command is not changed.</summary>
    [Fact]
    public async Task AnActiveCustomModeSPreambleIsPrependedToThePrompt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new ScriptedPromptChannel(Reply, Reply);
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = channel });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var runtime = fixture.Runtime("session-1");
            var preamble = new FixedPreamble("[review mode] ") { IsActive = true };
            runtime.PromptPreamble = preamble;

            (await runtime.Composer.SendFromAsync("check the diff").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming && channel.Prompts.Count == 1, "the first turn to end", Token).ConfigureAwait(true);

            channel.Prompts[0].ShouldBe("[review mode] check the diff");
            runtime.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(static block => block.Text).ShouldBe(["check the diff"]);

            preamble.IsActive = false;
            (await runtime.Composer.SendFromAsync("and the tests").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming && channel.Prompts.Count == 2, "the second turn to end", Token).ConfigureAwait(true);

            channel.Prompts[1].ShouldBe("and the tests", "an inactive mode adds nothing");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>omp runs a command from the start of the prompt, so text that starts with a slash is never put behind a preamble.</summary>
    [Fact]
    public async Task APreambleIsNotPutInFrontOfASlashPrompt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new ScriptedPromptChannel(Reply);
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = channel });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var runtime = fixture.Runtime("session-1");
            runtime.PromptPreamble = new FixedPreamble("[review mode] ") { IsActive = true };

            (await runtime.Composer.SendFromAsync("/compact now").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => channel.Prompts.Count == 1, "the turn to start", Token).ConfigureAwait(true);

            channel.Prompts[0].ShouldBe("/compact now");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>/duplicate asks the session's forker to fork the whole chat; the new session appears in the rail, is selected, and the original stays.</summary>
    [Fact]
    public async Task AForkOpensAndSelectsTheNewSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var forker = new RegisteringForker();
            var fixture = ShellFixture.CreateWithSession(new SessionScript
            {
                Prompts = new ScriptedPromptChannel(),
                SessionCommands = new FakeSessionCommands(),
                Forker = forker,
            });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            forker.Fixture = fixture;
            fixture.ShowWithServices(new ShellServices());

            (await fixture.Runtime("session-1").Composer.SendFromAsync("/duplicate").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => string.Equals(fixture.Catalog.SelectedId, "forked-1", StringComparison.Ordinal), "the forked session to be selected", Token).ConfigureAwait(true);

            forker.Requests.ShouldHaveSingleItem().ShouldBe(new ForkRequest("session-1", null));
            fixture.Catalog.Sessions.Select(static row => row.Id).ShouldBe(["session-1", "forked-1"], ignoreOrder: true);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A session with no forker offers no fork: /duplicate is listed but disabled.</summary>
    [Fact]
    public async Task ASessionWithoutAForkerCannotFork()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(), SessionCommands = new FakeSessionCommands() });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.ShowWithServices(new ShellServices());
            var runtime = fixture.Runtime("session-1");

            runtime.Forker.ShouldBeNull();
            runtime.Composer.Draft = "/";
            runtime.Composer.SlashPalette.ShouldNotBeNull().Entries.Single(static entry => string.Equals(entry.Name, "duplicate", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A usage event of a turn feeds the context ring and raises <c>ContextUsageChanged</c>; it adds nothing to the transcript.</summary>
    [Fact]
    public async Task AContextUsageEventFeedsTheRingAndRaisesTheEvent()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new ScriptedPromptChannel(new ScriptedTurn([new ContextUsageEvent(2_000, 8_000), new TextDeltaEvent("ok"), new CompletedEvent("end_turn")]));
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = channel, MentionSources = static () => [] });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var runtime = fixture.Runtime("session-1");
            var raised = 0;
            runtime.ContextUsageChanged += (_, _) => raised++;

            (await runtime.Composer.SendFromAsync("hello").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => raised == 1 && !runtime.Composer.IsStreaming, "the usage event", Token).ConfigureAwait(true);

            var ring = runtime.Composer.ContextRing.ShouldNotBeNull();
            ring.UsedTokens.ShouldBe(2_000);
            ring.WindowTokens.ShouldBe(8_000);
            ring.Percent.ShouldBe(25);
            runtime.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(static block => block.Markdown).ShouldNotContain(static text => text.Contains("2000", StringComparison.Ordinal));
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A hook notice and an interrupted turn raised on the live events reach the runtime's own events; a signal after disposal is dropped.</summary>
    [Fact]
    public async Task LiveEventsAreRaisedOnTheRuntimeUntilItIsDisposed()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var live = new SessionLiveEvents();
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(), LiveEvents = live });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var runtime = fixture.Runtime("session-1");
            var notices = new List<HookNotice>();
            var interrupted = new List<string?>();
            var statuses = 0;
            runtime.HookNoticeRaised += (_, e) => notices.Add(e.Notice);
            runtime.TurnInterrupted += (_, e) => interrupted.Add(e.NativeSessionId);
            runtime.RemoteStatusChanged += (_, _) => statuses++;
            var notice = new HookNotice
            {
                Kind = HookNoticeKind.Denied,
                SessionId = "session-1",
                At = DateTimeOffset.UnixEpoch,
                EventName = "PreToolUse",
                ToolCallId = "call-1",
                Message = "denied by hook guard: no rm",
            };

            live.NotifyHookNotice(notice);
            live.NotifyTurnInterrupted("native-7");

            notices.ShouldHaveSingleItem().ShouldBeSameAs(notice);
            interrupted.ShouldBe(["native-7"]);
            statuses.ShouldBe(1, "an interrupted turn tells the rail the remote session was interrupted");

            await runtime.DisposeAsync().ConfigureAwait(true);
            live.NotifyHookNotice(notice);
            live.NotifyTurnInterrupted("native-8");

            notices.Count.ShouldBe(1);
            interrupted.Count.ShouldBe(1);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A custom-mode preamble whose activity the test switches.</summary>
    private sealed class FixedPreamble(string prefix) : IPromptPreamble
    {
        public bool IsActive { get; set; }

        public string Apply(string prompt) => prefix + prompt;
    }

    /// <summary>A forker that registers the forked session in the shell's registry, as the engine set does, and records the requests.</summary>
    private sealed class RegisteringForker : ISessionForker
    {
        public ShellFixture? Fixture { get; set; }

        public List<ForkRequest> Requests { get; } = [];

        public bool CanForkFromMessage => true;

        public Task<ForkResult> ForkAsync(ForkRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Fixture.ShouldNotBeNull().AddSession("forked-1", "Copy of Agent one", script: new SessionScript { Prompts = new ScriptedPromptChannel() });
            return Task.FromResult(new ForkResult("forked-1", "Copy of Agent one"));
        }
    }
}
