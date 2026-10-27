using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>The desktop head's action host: which actions it can run, what it passes to each target, and what it says when an action cannot run or fails.</summary>
public sealed class DesktopSlashActionHostTests
{
    private static SlashActionRequest Request(string actionId, string argument = "", string typed = "") => new(actionId, argument, typed);

    /// <summary>Each action can run exactly when its target is supplied; the cloud hand-off and unknown actions never can.</summary>
    [Fact]
    public void AnActionCanRunWhenItsTargetIsSupplied()
    {
        var none = new DesktopSlashActionHost(new DesktopSlashTargets());
        var all = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            OpenSettings = _ => true,
            NewChat = _ => Task.CompletedTask,
            NewTerminal = _ => Task.CompletedTask,
            SetMode = (_, _) => Task.FromResult<string?>(null),
            PickModel = (_, _) => Task.FromResult(string.Empty),
            OpenSideChat = (_, _) => Task.CompletedTask,
            ShowHelp = _ => { },
        });
        string[] actions =
        [
            SlashActionIds.SettingsOpen, SlashActionIds.ChatNew, SlashActionIds.TerminalNew, SlashActionIds.ModeSet, SlashActionIds.ModelPick, SlashActionIds.SideOpen, SlashActionIds.HelpShow,
        ];

        actions.ShouldAllBe(action => !none.CanRun(action));
        actions.ShouldAllBe(action => all.CanRun(action));
        all.CanRun(SlashActionIds.CloudHandoff).ShouldBeFalse();
        all.CanRun("no.such.action").ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => all.CanRun(null!));
    }

    /// <summary>Settings opens with the section the catalogue names; a section the window lacks is a failure with a sentence.</summary>
    [Fact]
    public async Task SettingsOpensTheNamedSection()
    {
        var sections = new List<string>();
        var host = new DesktopSlashActionHost(new DesktopSlashTargets { OpenSettings = section => { sections.Add(section); return section.Length == 0 || string.Equals(section, "rules", StringComparison.Ordinal); } });
        var token = TestContext.Current.CancellationToken;

        var opened = await host.RunAsync(Request(SlashActionIds.SettingsOpen, "rules"), token).ConfigureAwait(true);
        var first = await host.RunAsync(Request(SlashActionIds.SettingsOpen), token).ConfigureAwait(true);
        var missing = await host.RunAsync(Request(SlashActionIds.SettingsOpen, "bedrock"), token).ConfigureAwait(true);

        sections.ShouldBe(["rules", string.Empty, "bedrock"]);
        opened.Outcome.ShouldBe(SlashActionOutcome.Completed);
        first.Outcome.ShouldBe(SlashActionOutcome.Completed);
        missing.Outcome.ShouldBe(SlashActionOutcome.Failed);
        missing.Message.ShouldBe("Settings has no bedrock page.", Case.Sensitive);
    }

    /// <summary>A new chat and a new terminal run their targets and say so.</summary>
    [Fact]
    public async Task NewChatAndNewTerminalRunTheirTargets()
    {
        var calls = new List<string>();
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            NewChat = _ => { calls.Add("chat"); return Task.CompletedTask; },
            NewTerminal = _ => { calls.Add("terminal"); return Task.CompletedTask; },
        });
        var token = TestContext.Current.CancellationToken;

        var chat = await host.RunAsync(Request(SlashActionIds.ChatNew), token).ConfigureAwait(true);
        var terminal = await host.RunAsync(Request(SlashActionIds.TerminalNew, typed: "ls"), token).ConfigureAwait(true);

        calls.ShouldBe(["chat", "terminal"]);
        chat.Message.ShouldBe("Started a new chat.", Case.Sensitive);
        terminal.Message.ShouldBe("Started a new terminal.", Case.Sensitive);
    }

    /// <summary>A mode switch that succeeds returns the typed text as the prompt to send in that mode; one that is refused fails with the reason and carries no prompt.</summary>
    [Fact]
    public async Task ASuccessfulModeSwitchCarriesTheTypedTextAsAFollowUp()
    {
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            SetMode = (mode, _) => Task.FromResult<string?>(string.Equals(mode, "plan", StringComparison.Ordinal) ? null : "The engine offers no " + mode + " mode."),
        });
        var token = TestContext.Current.CancellationToken;

        var plan = await host.RunAsync(Request(SlashActionIds.ModeSet, "plan", "add a cache"), token).ConfigureAwait(true);
        var bare = await host.RunAsync(Request(SlashActionIds.ModeSet, "plan"), token).ConfigureAwait(true);
        var ask = await host.RunAsync(Request(SlashActionIds.ModeSet, "ask", "why"), token).ConfigureAwait(true);

        plan.ShouldBe(new SlashActionResult(SlashActionOutcome.Completed, "Mode: plan.", "add a cache"));
        bare.FollowUpPrompt.ShouldBeNull();
        ask.Outcome.ShouldBe(SlashActionOutcome.Failed);
        ask.Message.ShouldBe("The engine offers no ask mode.", Case.Sensitive);
        ask.FollowUpPrompt.ShouldBeNull();
    }

    /// <summary>The model filter is the catalogue's argument when there is one (<c>/opus</c>) and the typed text otherwise (<c>/model sonnet</c>).</summary>
    [Fact]
    public async Task TheModelFilterIsTheArgumentOrTheTypedText()
    {
        var filters = new List<string>();
        var host = new DesktopSlashActionHost(new DesktopSlashTargets { PickModel = (filter, _) => { filters.Add(filter); return Task.FromResult("Model: " + filter + "."); } });
        var token = TestContext.Current.CancellationToken;

        var opus = await host.RunAsync(Request(SlashActionIds.ModelPick, "opus", "ignored"), token).ConfigureAwait(true);
        var typed = await host.RunAsync(Request(SlashActionIds.ModelPick, typed: "sonnet"), token).ConfigureAwait(true);

        filters.ShouldBe(["opus", "sonnet"]);
        opus.Message.ShouldBe("Model: opus.", Case.Sensitive);
        typed.Message.ShouldBe("Model: sonnet.", Case.Sensitive);
    }

    /// <summary>A side chat gets the typed question; help gets the typed text.</summary>
    [Fact]
    public async Task SideChatAndHelpGetTheTypedText()
    {
        string? question = null;
        string? helpFor = null;
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            OpenSideChat = (text, _) => { question = text; return Task.CompletedTask; },
            ShowHelp = text => helpFor = text,
        });
        var token = TestContext.Current.CancellationToken;

        await host.RunAsync(Request(SlashActionIds.SideOpen, typed: "why is the build red?"), token).ConfigureAwait(true);
        await host.RunAsync(Request(SlashActionIds.HelpShow, typed: "pl"), token).ConfigureAwait(true);

        question.ShouldBe("why is the build red?", Case.Sensitive);
        helpFor.ShouldBe("pl", Case.Sensitive);
    }

    /// <summary>The cloud hand-off says cloud agents are phase 60; an action with no target says the window cannot run it.</summary>
    [Fact]
    public async Task UnavailableActionsSayWhy()
    {
        var host = new DesktopSlashActionHost(new DesktopSlashTargets());
        var token = TestContext.Current.CancellationToken;

        var cloud = await host.RunAsync(Request(SlashActionIds.CloudHandoff, typed: "task"), token).ConfigureAwait(true);
        var chat = await host.RunAsync(Request(SlashActionIds.ChatNew), token).ConfigureAwait(true);

        cloud.ShouldBe(SlashActionResult.NotAvailable("cloud agents: phase 60"));
        chat.ShouldBe(SlashActionResult.NotAvailable(SlashPaletteCopy.ActionNotInThisWindow));
    }

    /// <summary>A target that throws is a failed action with the message, and a cancelled action is not swallowed.</summary>
    [Fact]
    public async Task AThrowingTargetFailsTheActionButCancellationPropagates()
    {
        var host = new DesktopSlashActionHost(new DesktopSlashTargets
        {
            NewChat = _ => throw new InvalidOperationException("no workspace"),
            NewTerminal = token => Task.FromCanceled(token),
        });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        var failed = await host.RunAsync(Request(SlashActionIds.ChatNew), TestContext.Current.CancellationToken).ConfigureAwait(true);

        failed.Outcome.ShouldBe(SlashActionOutcome.Failed);
        failed.Message.ShouldBe("The command failed: no workspace", Case.Sensitive);
        await Should.ThrowAsync<TaskCanceledException>(() => host.RunAsync(Request(SlashActionIds.TerminalNew), cancelled.Token)).ConfigureAwait(true);
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public async Task NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => new DesktopSlashActionHost(null!));
        await Should.ThrowAsync<ArgumentNullException>(() => new DesktopSlashActionHost(new DesktopSlashTargets()).RunAsync(null!, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }
}
