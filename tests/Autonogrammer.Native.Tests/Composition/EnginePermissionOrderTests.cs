using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Send;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>
/// The order in which a composed engine answers a tool permission request from omp: a hook's deny first, then the run mode (its deny rules, its mode and the approval-policy ceiling),
/// and only a request the run mode leaves open reaches the user. The peer is a scripted omp that sends <c>session/request_permission</c>; the answer is the frame the client sends back.
/// </summary>
public sealed class EnginePermissionOrderTests
{
    private const long RequestId = 910;

    private static readonly OmpLaunchPlan Plan = new()
    {
        ExtraArguments = [],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        McpServers = [],
        WorkingDirectory = "/work",
        PlanningEvents = [],
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A hook that denies stops a call the approval policy admits: omp is told no, the user is never asked and the run mode never looks at it.</summary>
    [Fact]
    public async Task AHookDenyStopsACallTheApprovalPolicyWouldAdmit()
    {
        using var tree = new TempTree();
        var gate = new DenyingHookGate();
        var audit = new RecordingAudit();
        var rig = await Rig.OpenAsync(tree, ["read"], gate, RunMode.AutoReview, audit).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        var answer = await rig.AskAsync("read", null).ConfigureAwait(true);

        answer.ShouldBe("reject");
        gate.Requests.ShouldHaveSingleItem().Kind.ShouldBe("read");
        rig.Engine.Resolver.TrySettle(RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), PermissionDecision.Allow).ShouldBeFalse("the user was never asked");
        audit.Entries.ShouldBeEmpty("the run mode never saw the request");
    }

    /// <summary>The same request with a hook that allows is not stopped: it reaches the user, who admits it. The deny above is the hook's doing.</summary>
    [Fact]
    public async Task AHookThatAllowsLeavesTheRequestToTheRunModeAndTheUser()
    {
        using var tree = new TempTree();
        var gate = new DenyingHookGate { Deny = false };
        var audit = new RecordingAudit();
        var rig = await Rig.OpenAsync(tree, ["read"], gate, RunMode.AutoReview, audit).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        var answer = await rig.AskAsync("read", null, allowAsUser: true).ConfigureAwait(true);

        answer.ShouldBe("allow");
        gate.Requests.Count.ShouldBe(1);
        audit.Entries.ShouldHaveSingleItem().Decision.Outcome.ShouldBe(RunModeOutcome.Ask);
    }

    /// <summary>A hook's deny holds in Run Everything too: the mode that admits every kind of call does not override a hook.</summary>
    [Fact]
    public async Task AHookDenyHoldsInRunEverything()
    {
        using var tree = new TempTree();
        var gate = new DenyingHookGate();
        var rig = await Rig.OpenAsync(tree, ["read"], gate, RunMode.RunEverything, new RecordingAudit()).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        (await rig.AskAsync("execute", "make deploy").ConfigureAwait(true)).ShouldBe("reject");
    }

    /// <summary>A shell deny rule of the permission files stops a command before the user is asked, in the default mode, and the audit records the rule as the reason.</summary>
    [Fact]
    public async Task ARunModeDenyRuleStopsACommandBeforeTheUserIsAsked()
    {
        using var tree = new TempTree();
        WritePermissions(tree, "{\"permissions\":{\"deny\":[\"Shell(rm)\"]}}");
        var audit = new RecordingAudit();
        var rig = await Rig.OpenAsync(tree, ["execute"], null, RunMode.AutoReview, audit).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        var answer = await rig.AskAsync("execute", "rm -rf build").ConfigureAwait(true);

        answer.ShouldBe("reject");
        rig.Engine.Resolver.TrySettle(RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), PermissionDecision.Allow).ShouldBeFalse("the user was never asked");
        var entry = audit.Entries.ShouldHaveSingleItem();
        entry.Decision.Outcome.ShouldBe(RunModeOutcome.Deny);
        entry.Decision.Stage.ShouldBe(RunModeStage.DenyRule);
        entry.Subject.ShouldBe("rm -rf build");
    }

    /// <summary>A command no rule decides is left to the user: the request waits for the user's answer, which is what goes back to omp.</summary>
    [Fact]
    public async Task ACommandNoRuleDecidesIsAskedOfTheUser()
    {
        using var tree = new TempTree();
        WritePermissions(tree, "{\"permissions\":{\"deny\":[\"Shell(rm)\"]}}");
        var audit = new RecordingAudit();
        var rig = await Rig.OpenAsync(tree, ["execute"], null, RunMode.AutoReview, audit).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        (await rig.AskAsync("execute", "make build", allowAsUser: true).ConfigureAwait(true)).ShouldBe("allow");

        audit.Entries.ShouldHaveSingleItem().Decision.Outcome.ShouldBe(RunModeOutcome.Ask);
    }

    /// <summary>A kind the approval policy does not admit is refused by the policy itself, outside Run Everything: neither the user nor the run mode is consulted.</summary>
    [Fact]
    public async Task TheApprovalPolicyCeilingRefusesAKindItDoesNotAdmit()
    {
        using var tree = new TempTree();
        var audit = new RecordingAudit();
        var rig = await Rig.OpenAsync(tree, ["read"], null, RunMode.AutoReview, audit).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        (await rig.AskAsync("fetch", null).ConfigureAwait(true)).ShouldBe("reject");

        rig.Engine.Resolver.TrySettle(RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), PermissionDecision.Allow).ShouldBeFalse("the user was never asked");
        audit.Entries.ShouldBeEmpty("the approval policy refused the kind before the run mode was consulted");
    }

    /// <summary>Run Everything admits every kind of call omp can ask about, whatever kinds the approval policy was composed with, and never asks the user.</summary>
    [Theory]
    [InlineData("read")]
    [InlineData("edit")]
    [InlineData("delete")]
    [InlineData("move")]
    [InlineData("search")]
    [InlineData("execute")]
    [InlineData("think")]
    [InlineData("fetch")]
    [InlineData("switch_mode")]
    [InlineData("other")]
    public async Task RunEverythingAdmitsEveryKind(string kind)
    {
        RunModeLaunchMapper.AcpToolKinds.ShouldContain(kind, StringComparer.Ordinal);
        using var tree = new TempTree();
        var audit = new RecordingAudit();
        var rig = await Rig.OpenAsync(tree, [], null, RunMode.RunEverything, audit).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        (await rig.AskAsync(kind, string.Equals(kind, "execute", StringComparison.Ordinal) ? "make deploy" : null).ConfigureAwait(true)).ShouldBe("allow");

        audit.Entries.ShouldHaveSingleItem().Decision.Outcome.ShouldBe(RunModeOutcome.Allow);
    }

    /// <summary>A deny rule still applies in Run Everything: the mode runs every call except the ones the user's own files forbid.</summary>
    [Fact]
    public async Task ADenyRuleStillStopsACommandInRunEverything()
    {
        using var tree = new TempTree();
        WritePermissions(tree, "{\"permissions\":{\"deny\":[\"Shell(rm)\"]}}");
        var rig = await Rig.OpenAsync(tree, [], null, RunMode.RunEverything, new RecordingAudit()).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);

        (await rig.AskAsync("execute", "rm -rf build").ConfigureAwait(true)).ShouldBe("reject");
    }

    private static void WritePermissions(TempTree tree, string json)
    {
        var file = SafetyFileLocations.Permissions(tree.Root, null).First(static location => location.Path.EndsWith(SafetyFileLocations.PermissionsFileName, StringComparison.Ordinal)).Path;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, json);
    }

    /// <summary>A composed engine connected to a scripted omp, with the run mode holder it reads its policy from.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly RunModePolicyHolder _holder;

        private Rig(EngineComposition engine, PermissionPeer peer, RunModePolicyHolder holder)
        {
            Engine = engine;
            Peer = peer;
            _holder = holder;
        }

        public EngineComposition Engine { get; }

        public PermissionPeer Peer { get; }

        public static async Task<Rig> OpenAsync(TempTree tree, string[] allowedKinds, IAcpHookGate? gate, RunMode mode, IRunModeAuditSink audit)
        {
            var hub = new RunModeHub(tree.Root, new FileRunModeStore(tree.Root), static _ => true);
            var holder = hub.Open(tree.Workspace);
            _ = await hub.Store.SaveAsync(mode, Token).ConfigureAwait(true);
            await hub.RefreshAllAsync(Token).ConfigureAwait(true);
            var peer = new PermissionPeer();
            var engine = EngineComposition.Compose(
                allowedKinds,
                Plan,
                peer.TransportFactory,
                options: new EngineCompositionOptions { HookGate = gate, RunModePolicy = () => holder.Current, RunModeAudit = audit });
            await engine.Host.ConnectAsync(Token).ConfigureAwait(true);
            return new Rig(engine, peer, holder);
        }

        /// <summary>Has omp ask for a permission and returns the option id the client answered with. With <paramref name="allowAsUser"/> the user admits a request that reaches them.</summary>
        public async Task<string> AskAsync(string kind, string? command, bool allowAsUser = false)
        {
            Peer.Ask(kind, command);
            if (allowAsUser)
            {
                await ShellWait.UntilAsync(
                    () => Engine.Resolver.TrySettle(RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture), PermissionDecision.Allow),
                    "the request to reach the user",
                    Token).ConfigureAwait(true);
            }

            var answer = await Peer.WaitAnswerAsync().ConfigureAwait(true);
            answer["result"]!["outcome"]!["outcome"]!.GetValue<string>().ShouldBe("selected");
            return answer["result"]!["outcome"]!["optionId"]!.GetValue<string>();
        }

        public async ValueTask DisposeAsync()
        {
            await Engine.Host.DisposeAsync().ConfigureAwait(true);
            _holder.Dispose();
        }
    }

    /// <summary>A scripted omp: it opens <c>native-1</c>, and on request sends <c>session/request_permission</c> and keeps the client's answer.</summary>
    private sealed class PermissionPeer
    {
        private readonly TaskCompletionSource<JsonObject> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Action<IEnumerable<byte[]>>? _push;

        public Func<AcpClientOptions, IAcpTransport> TransportFactory => _ =>
        {
            var transport = new ScriptedAgentTransport(Respond);
            _push = transport.Push;
            return transport;
        };

        public async Task<JsonObject> WaitAnswerAsync() => await _answer.Task.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);

        public void Ask(string kind, string? command)
        {
            var toolCall = new JsonObject { ["toolCallId"] = "call-1", ["title"] = command ?? kind, ["kind"] = kind };
            if (command is not null)
            {
                toolCall["rawInput"] = new JsonObject { ["command"] = command };
            }

            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = RequestId,
                ["method"] = "session/request_permission",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = "native-1",
                    ["toolCall"] = toolCall,
                    ["options"] = new JsonArray(
                        new JsonObject { ["optionId"] = "allow", ["name"] = "Allow", ["kind"] = "allow_once" },
                        new JsonObject { ["optionId"] = "reject", ["name"] = "Reject", ["kind"] = "reject_once" }),
                },
            };
            _push.ShouldNotBeNull().Invoke([Encoding.UTF8.GetBytes(request.ToJsonString())]);
        }

        private static byte[] Result(long id, string result) => Encoding.UTF8.GetBytes($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}");

        private IEnumerable<byte[]> Respond(byte[] frame)
        {
            var node = JsonNode.Parse(frame)!.AsObject();
            var id = node["id"]?.GetValue<long>() ?? 0;
            switch (node["method"]?.GetValue<string>())
            {
                case "initialize":
                    return [Result(id, "{\"protocolVersion\":1,\"agentCapabilities\":{\"loadSession\":true},\"authMethods\":[{\"id\":\"agent\",\"name\":\"Use existing local credentials\"}]}")];
                case "authenticate":
                case "session/close":
                    return [Result(id, "{}")];
                case "session/new":
                    return [Result(id, "{\"sessionId\":\"native-1\"}")];
                case null when id == RequestId:
                    _ = _answer.TrySetResult(node);
                    return [];
                default:
                    return [];
            }
        }
    }

    /// <summary>A hook gate that denies every permission request (or allows it) and records the requests it was asked about.</summary>
    private sealed class DenyingHookGate : IAcpHookGate
    {
        public bool Deny { get; init; } = true;

        public List<AcpPermissionRequest> Requests { get; } = [];

        public ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult(AcpHookVerdict.Allow);

        public ValueTask<AcpHookVerdict> BeforePermissionAsync(AcpPermissionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(Deny ? new AcpHookVerdict { Denied = true, UserMessage = "no deploys from here" } : AcpHookVerdict.Allow);
        }

        public ValueTask<AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken) => ValueTask.FromResult(AcpHookVerdict.Allow);

        public ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Observe(AcpHookSignal signal)
        {
        }

        public ValueTask<AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken) => ValueTask.FromResult(AcpTurnEndDecision.End);

        public ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>An audit sink that keeps the entries it was given.</summary>
    private sealed class RecordingAudit : IRunModeAuditSink
    {
        public List<RunModeAuditEntry> Entries { get; } = [];

        public void Record(RunModeAuditEntry entry) => Entries.Add(entry);
    }
}
