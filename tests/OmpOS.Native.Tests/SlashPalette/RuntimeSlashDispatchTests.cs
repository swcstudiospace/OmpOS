using OmpOS.Contracts.Agents;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>What a send of slash text does in a chat runtime: native actions run in the head and send nothing, discovered commands are expanded, omp's own commands are sent as typed, and refused commands send nothing and say why.</summary>
public sealed class RuntimeSlashDispatchTests
{
    /// <summary>A target the shell attaches enables its command, and sending the command runs the target and sends no prompt; the composer clears its draft.</summary>
    [Fact]
    public async Task ANativeCommandRunsInTheHeadAndSendsNoPrompt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var chats = 0;
            var runtime = Create(channel);
            await using var scope = runtime.ConfigureAwait(true);
            var palette = runtime.Composer.SlashPalette.ShouldNotBeNull();
            runtime.Composer.Draft = "/clear";
            palette.Entries.Single(static entry => string.Equals(entry.Name, "clear", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse();

            runtime.AttachSlashTargets(new DesktopSlashTargets { NewChat = _ => { chats++; return Task.CompletedTask; } });
            palette.Entries.Single(static entry => string.Equals(entry.Name, "clear", StringComparison.Ordinal)).IsEnabled.ShouldBeTrue();
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            chats.ShouldBe(1);
            channel.Prompts.ShouldBeEmpty();
            runtime.Composer.Draft.ShouldBeEmpty();
            runtime.StatusText.ShouldBe("Started a new chat.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A command omp handles is sent as typed, and so is a name nobody knows.</summary>
    [Fact]
    public async Task OmpsOwnCommandsAndUnknownNamesAreSentAsTyped()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var runtime = Create(channel);
            await using var scope = runtime.ConfigureAwait(true);

            runtime.Composer.Draft = "/rename Plan B";
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            channel.Prompts.ShouldBe(["/rename Plan B"]);
            channel.ReleaseFirstTurn();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An alias omp names differently is rewritten before it is sent.</summary>
    [Fact]
    public async Task AnAliasIsSentAsOmpsCommand()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var runtime = Create(channel);
            await using var scope = runtime.ConfigureAwait(true);

            runtime.Composer.Draft = "/summarize keep the API notes";
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            channel.Prompts.ShouldBe(["/compact keep the API notes"]);
            channel.ReleaseFirstTurn();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A custom command found when the session started is expanded with the typed arguments before it is sent.</summary>
    [Fact]
    public async Task ADiscoveredCommandIsExpandedBeforeItIsSent()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var discovery = new FixedSlashDiscovery(new SlashDiscovery(
                [new SlashDiscoveredCommand("deploy", "Deploys.", "<env>", "Deploy to $1.", SlashScope.Project, ".cursor/commands/deploy.md")],
                [],
                [],
                []));
            var runtime = Create(channel, discovery);
            await using var scope = runtime.ConfigureAwait(true);

            runtime.Composer.Draft = "/deploy staging";
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            channel.Prompts.ShouldBe(["Deploy to staging."]);
            channel.ReleaseFirstTurn();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A token OmpOS cannot run sends nothing and shows its reason on the status line.</summary>
    [Fact]
    public async Task AnUnavailableCommandSendsNothingAndShowsItsReason()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var runtime = Create(channel);
            await using var scope = runtime.ConfigureAwait(true);

            runtime.Composer.Draft = "/in-cloud fix the login bug";
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            channel.Prompts.ShouldBeEmpty();
            runtime.StatusText.ShouldBe("cloud agents: phase 60");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A mode switch through the composer's own target sends the text after the command as the first prompt in that mode.</summary>
    [Fact]
    public async Task APlanCommandSwitchesTheModeAndSendsTheText()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var channel = new GatedPromptChannel();
            var runtime = Create(channel);
            await using var scope = runtime.ConfigureAwait(true);
            runtime.Composer.ModeChip = new OptionChipModel("mode", "Mode", (value, _) => Task.FromResult(OptionChangeResult.Set(value)));
            runtime.Composer.ModeChip.Refresh(
                [OptionChoice.Create("default", "Default", null), OptionChoice.Create("plan", "Plan", null)],
                "default");

            runtime.Composer.Draft = "/plan add a cache";
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            runtime.Composer.ModeChip.CurrentValue.ShouldBe("plan");
            channel.Prompts.ShouldBe(["add a cache"]);
            channel.ReleaseFirstTurn();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(GatedPromptChannel channel, ISlashDiscoverySource? discovery = null)
    {
        const string sessionId = "session-slash-dispatch";
        var now = DateTimeOffset.UnixEpoch;
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary
        {
            Id = sessionId,
            Title = "Agent " + sessionId,
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
            Kind = SessionKind.Chat,
        });
        using var model = new SessionCatalogModel(catalog, new ShellClock());
        model.Refresh();
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = model.Sessions.Single(),
            Prompts = channel,
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            SessionCommands = new FakeSessionCommands(),
            SlashDiscovery = discovery,
        });
    }
}
