using System.Text.Json;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// The ACP tap with real hook files and a runner that starts nothing: which events it fires and when, that a deny rejects before the policy, that it stands
/// aside for the omp extension, that a follow-up is bounded by its limit, and that the notices name the hook and the tool call.
/// </summary>
public sealed class AcpHookTapTests
{
    private const string HooksJson =
        "{\"version\":1,\"hooks\":{"
        + "\"preToolUse\":[{\"command\":\"./pre.sh\",\"id\":\"pre\"}],"
        + "\"beforeShellExecution\":[{\"command\":\"./shell.sh\",\"id\":\"shell\"}],"
        + "\"postToolUse\":[{\"command\":\"./post.sh\",\"id\":\"post\"}],"
        + "\"postToolUseFailure\":[{\"command\":\"./failed.sh\",\"id\":\"failed\"}],"
        + "\"afterShellExecution\":[{\"command\":\"./aftershell.sh\",\"id\":\"aftershell\"}],"
        + "\"beforeSubmitPrompt\":[{\"command\":\"./prompt.sh\",\"id\":\"prompt\"}],"
        + "\"stop\":[{\"command\":\"./stop.sh\",\"id\":\"stop\",\"loop_limit\":1}],"
        + "\"sessionStart\":[{\"command\":\"./start.sh\",\"id\":\"start\"}],"
        + "\"sessionEnd\":[{\"command\":\"./end.sh\",\"id\":\"end\"}],"
        + "\"afterAgentResponse\":[{\"command\":\"./response.sh\",\"id\":\"response\"}],"
        + "\"afterAgentThought\":[{\"command\":\"./thought.sh\",\"id\":\"thought\"}],"
        + "\"beforeReadFile\":[{\"command\":\"./read.sh\",\"id\":\"read\"}],"
        + "\"afterFileEdit\":[{\"command\":\"./edit.sh\",\"id\":\"edit\"}]"
        + "}}";

    private static string Permission(string kind, string title, string rawInput) =>
        "{\"sessionId\":\"x\",\"toolCall\":{\"toolCallId\":\"c1\",\"title\":\"" + title + "\",\"kind\":\"" + kind + "\",\"rawInput\":" + rawInput + "}}";

    private static AcpPermissionRequest ShellRequest() =>
        new("c1", "git push origin main", "execute", Permission("execute", "git push origin main", "{\"command\":\"git push origin main\"}"));

    private static void Script(ScriptedHookRunner runner, Func<string, string?> answers) =>
        runner.Behavior = (invocation, _) => Task.FromResult(ScriptedHookRunner.Say(answers(invocation.Command ?? string.Empty) ?? "{}"));

    private static IEnumerable<string> Commands(ScriptedHookRunner runner) => runner.Invocations.Select(static i => i.Command ?? string.Empty);

    private static string InputOf(ScriptedHookRunner runner, string command) =>
        ScriptedHookRunner.InputOf(runner.Invocations.Single(i => string.Equals(i.Command, command, StringComparison.Ordinal)));

    private static async Task<(HooksServiceRig Rig, IHooksSession Session, IAcpHookGate Gate)> StartAsync(bool withPlugin, CancellationToken ct)
    {
        var rig = await HooksServiceRig.CreateAsync(HooksJson, withPlugin, ct).ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        return (rig, session, session.Gate.ShouldNotBeNull());
    }

    /// <summary>A hook that denies a shell command rejects the permission request, with the hook's messages; the hooks of both events ran, preToolUse first.</summary>
    [Fact]
    public async Task ADenyOfAShellHookRejectsThePermissionRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        Script(rig.Runner, command => string.Equals(command, "./shell.sh", StringComparison.Ordinal) ? "{\"permission\":\"deny\",\"user_message\":\"no force pushes\",\"agent_message\":\"policy forbids it\"}" : null);

        var verdict = await gate.BeforePermissionAsync(ShellRequest(), ct).ConfigureAwait(true);

        verdict.Denied.ShouldBeTrue();
        verdict.UserMessage.ShouldBe("no force pushes");
        verdict.AgentMessage.ShouldBe("policy forbids it");
        Commands(rig.Runner).ShouldBe(["./pre.sh", "./shell.sh"]);
        InputOf(rig.Runner, "./pre.sh").ShouldContain("\"tool_name\":\"Shell\"", Case.Sensitive);
        InputOf(rig.Runner, "./shell.sh").ShouldContain("\"command\":\"git push origin main\"", Case.Sensitive);
    }

    /// <summary>An ask is not a refusal: the request goes on to the policy and the ordinary approval prompt.</summary>
    [Fact]
    public async Task AnAskLeavesThePermissionToTheOrdinaryApprovalFlow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        Script(rig.Runner, command => string.Equals(command, "./shell.sh", StringComparison.Ordinal) ? "{\"permission\":\"ask\"}" : null);

        var verdict = await gate.BeforePermissionAsync(ShellRequest(), ct).ConfigureAwait(true);

        verdict.Denied.ShouldBeFalse();
    }

    /// <summary>The denial is announced as a notice that names the hook and the tool call and carries the hook's message for the user.</summary>
    [Fact]
    public async Task ADenialRaisesANoticeThatNamesTheHookAndTheToolCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var notices = new List<HookNotice>();
        session.NoticeRaised += (_, e) => notices.Add(e.Notice);
        Script(rig.Runner, command => string.Equals(command, "./shell.sh", StringComparison.Ordinal) ? "{\"permission\":\"deny\",\"user_message\":\"no force pushes\"}" : null);

        await gate.BeforePermissionAsync(ShellRequest(), ct).ConfigureAwait(true);

        var notice = notices.ShouldHaveSingleItem();
        notice.Kind.ShouldBe(HookNoticeKind.Denied);
        notice.ToolCallId.ShouldBe("c1");
        notice.Message.ShouldBe("no force pushes");
        notice.HookId.ShouldNotBeNull().ShouldEndWith(":shell", Case.Sensitive);
    }

    /// <summary>With the omp extension connected the tap stands aside for everything the extension sees: no hook is spawned for a permission request, a read, a write or an observation.</summary>
    [Fact]
    public async Task WithTheExtensionConnectedTheTapLeavesToolEventsToIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        Script(rig.Runner, _ => "{\"permission\":\"deny\"}");

        (await gate.BeforePermissionAsync(ShellRequest(), ct).ConfigureAwait(true)).Denied.ShouldBeFalse();
        (await gate.BeforeReadFileAsync("/work/a.txt", "text", ct).ConfigureAwait(true)).Denied.ShouldBeFalse();
        await gate.AfterFileWriteAsync("/work/a.txt", "old", "new", ct).ConfigureAwait(true);
        await gate.SessionOpenedAsync("x", resumed: false, ct).ConfigureAwait(true);
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentResponse, Text = "hello" });
        (await gate.TurnEndedAsync("end_turn", ct).ConfigureAwait(true)).FollowUpPrompt.ShouldBeNull();
        await gate.SessionClosedAsync("user_close", ct).ConfigureAwait(true);

        rig.Runner.SpawnCount.ShouldBe(0);
    }

    /// <summary>Even with the extension the prompt is the tap's: a hook that answers continue false stops it, and the notice says why.</summary>
    [Fact]
    public async Task APromptHookCanStopThePromptEvenWithTheExtension()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var notices = new List<HookNotice>();
        session.NoticeRaised += (_, e) => notices.Add(e.Notice);
        Script(rig.Runner, command => string.Equals(command, "./prompt.sh", StringComparison.Ordinal) ? "{\"continue\":false,\"user_message\":\"not on a Friday\"}" : null);

        var verdict = await gate.BeforePromptAsync("deploy to production", ct).ConfigureAwait(true);

        verdict.Denied.ShouldBeTrue();
        verdict.UserMessage.ShouldBe("not on a Friday");
        verdict.PromptContext.ShouldBeNull();
        ScriptedHookRunner.InputOf(rig.Runner.Invocations.Single()).ShouldContain("\"prompt\":\"deploy to production\"", Case.Sensitive);
        notices.ShouldHaveSingleItem().Message.ShouldBe("not on a Friday");
    }

    /// <summary>The context a sessionStart hook adds is shown as a notice and goes in front of the next prompt, once.</summary>
    [Fact]
    public async Task SessionStartContextIsCarriedToTheNextPromptOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var notices = new List<HookNotice>();
        session.NoticeRaised += (_, e) => notices.Add(e.Notice);
        Script(rig.Runner, command => string.Equals(command, "./start.sh", StringComparison.Ordinal) ? "{\"additional_context\":\"Team rule: use tabs.\"}" : "{\"continue\":true}");

        await gate.SessionOpenedAsync("x", resumed: false, ct).ConfigureAwait(true);
        var first = await gate.BeforePromptAsync("hello", ct).ConfigureAwait(true);
        var second = await gate.BeforePromptAsync("again", ct).ConfigureAwait(true);

        first.Denied.ShouldBeFalse();
        first.PromptContext.ShouldBe("Team rule: use tabs.");
        second.PromptContext.ShouldBeNull();
        var context = notices.ShouldHaveSingleItem();
        context.Kind.ShouldBe(HookNoticeKind.Context);
        context.Message.ShouldBe("Team rule: use tabs.");
    }

    /// <summary>A stop hook's follow-up continues the conversation up to its loop limit; a new user prompt gives it a fresh budget; an aborted turn is never continued.</summary>
    [Fact]
    public async Task AStopFollowUpIsBoundedByItsLoopLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var notices = new List<HookNotice>();
        session.NoticeRaised += (_, e) => notices.Add(e.Notice);
        Script(rig.Runner, command => string.Equals(command, "./stop.sh", StringComparison.Ordinal) ? "{\"followup_message\":\"keep going\"}" : "{\"continue\":true}");

        var first = await gate.TurnEndedAsync("end_turn", ct).ConfigureAwait(true);
        first.FollowUpPrompt.ShouldBe("keep going");
        first.Notice.ShouldBe("Hook continued the conversation (1 of 1)");
        (await gate.TurnEndedAsync("end_turn", ct).ConfigureAwait(true)).FollowUpPrompt.ShouldBeNull();

        await gate.BeforePromptAsync("a new message", ct).ConfigureAwait(true);
        (await gate.TurnEndedAsync("end_turn", ct).ConfigureAwait(true)).FollowUpPrompt.ShouldBe("keep going");

        await gate.BeforePromptAsync("another message", ct).ConfigureAwait(true);
        (await gate.TurnEndedAsync("cancelled", ct).ConfigureAwait(true)).FollowUpPrompt.ShouldBeNull();
        ScriptedHookRunner.InputOf(rig.Runner.Invocations.First(static i => string.Equals(i.Command, "./stop.sh", StringComparison.Ordinal))).ShouldContain("\"status\":\"completed\"", Case.Sensitive);
        notices.Where(static n => n.Kind == HookNoticeKind.FollowUp).Select(static n => n.Count).ShouldBe([1, 1]);
    }

    /// <summary>A tool call and its completion fire postToolUse and, for a shell command, afterShellExecution with the command, the output and the tool call id; a failure fires postToolUseFailure.</summary>
    [Fact]
    public async Task ToolObservationsFireThePostEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        using var input = JsonDocument.Parse("{\"command\":\"ls -la\"}");
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.ToolCall, ToolCallId = "t1", Title = "ls", ToolKind = "execute", RawInput = input.RootElement.Clone() });
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.ToolCallUpdate, ToolCallId = "t1", Status = "completed", Text = "a.txt" });
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.ToolCall, ToolCallId = "t2", Title = "cat", ToolKind = "read" });
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.ToolCallUpdate, ToolCallId = "t2", Status = "failed", Text = "no such file" });
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.ToolCallUpdate, ToolCallId = "never-started", Status = "completed" });

        await gate.SessionClosedAsync("user_close", ct).ConfigureAwait(true);

        Commands(rig.Runner).ShouldBe(["./post.sh", "./aftershell.sh", "./failed.sh", "./end.sh"]);
        var post = InputOf(rig.Runner, "./post.sh");
        post.ShouldContain("\"tool_name\":\"Shell\"", Case.Sensitive);
        post.ShouldContain("\"tool_use_id\":\"t1\"", Case.Sensitive);
        var shell = InputOf(rig.Runner, "./aftershell.sh");
        shell.ShouldContain("\"command\":\"ls -la\"", Case.Sensitive);
        shell.ShouldContain("\"output\":\"a.txt\"", Case.Sensitive);
        var failure = InputOf(rig.Runner, "./failed.sh");
        failure.ShouldContain("\"error_message\":\"no such file\"", Case.Sensitive);
        failure.ShouldContain("\"failure_type\":\"error\"", Case.Sensitive);
    }

    /// <summary>An aggregated assistant message and thinking block fire afterAgentResponse and afterAgentThought with their text and the thinking duration; a plan fires nothing.</summary>
    [Fact]
    public async Task MessagesAndThoughtsFireTheirEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentThought, Text = "weighing options", Duration = TimeSpan.FromMilliseconds(1500) });
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentResponse, Text = "Done." });
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.Plan, Text = "- [ ] step" });

        await gate.SessionClosedAsync("user_close", ct).ConfigureAwait(true);

        Commands(rig.Runner).ShouldBe(["./thought.sh", "./response.sh", "./end.sh"]);
        var thought = InputOf(rig.Runner, "./thought.sh");
        thought.ShouldContain("\"text\":\"weighing options\"", Case.Sensitive);
        thought.ShouldContain("\"duration_ms\":1500", Case.Sensitive);
        InputOf(rig.Runner, "./response.sh").ShouldContain("\"text\":\"Done.\"", Case.Sensitive);
    }

    /// <summary>A hook that denies a read keeps the content from the agent; a write fires afterFileEdit with the part of the text that changed.</summary>
    [Fact]
    public async Task ReadsCanBeDeniedAndWritesReportTheirEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        Script(rig.Runner, command => string.Equals(command, "./read.sh", StringComparison.Ordinal) ? "{\"permission\":\"deny\",\"user_message\":\"private\"}" : null);

        var read = await gate.BeforeReadFileAsync(Path.Combine(rig.Project, "secret.txt"), "the secret", ct).ConfigureAwait(true);
        await gate.AfterFileWriteAsync(Path.Combine(rig.Project, "a.txt"), "hello world", "hello there", ct).ConfigureAwait(true);

        read.Denied.ShouldBeTrue();
        read.UserMessage.ShouldBe("private");
        InputOf(rig.Runner, "./read.sh").ShouldContain("\"content\":\"the secret\"", Case.Sensitive);
        var edit = InputOf(rig.Runner, "./edit.sh");
        edit.ShouldContain("\"old_string\":\"world\"", Case.Sensitive);
        edit.ShouldContain("\"new_string\":\"there\"", Case.Sensitive);
    }

    /// <summary>Closing the session runs the sessionEnd hook once, with the reason and after the observations that were still queued.</summary>
    [Fact]
    public async Task ClosingTheSessionRunsSessionEndAfterTheQueuedObservations()
    {
        var ct = TestContext.Current.CancellationToken;
        var (rig, session, gate) = await StartAsync(withPlugin: false, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentResponse, Text = "bye" });

        await gate.SessionClosedAsync("user_close", ct).ConfigureAwait(true);

        Commands(rig.Runner).ShouldBe(["./response.sh", "./end.sh"]);
        InputOf(rig.Runner, "./end.sh").ShouldContain("\"reason\":\"user_close\"", Case.Sensitive);
    }
}
