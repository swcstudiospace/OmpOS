using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>The service's real bridge over real loopback HTTP: the wire contract of the extension's requests and the host's decisions, from a client written in C#.</summary>
public sealed class HookBridgeIntegrationTests
{
    private static async Task<JsonElement> PostAsync(HookBridgeEndpoint endpoint, string body, CancellationToken ct)
    {
        using var handler = new HttpClientHandler { UseProxy = false, CheckCertificateRevocationList = true };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Environment[HookBridgeEnvironment.Url])
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.Environment[HookBridgeEnvironment.Token]);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        response.IsSuccessStatusCode.ShouldBeTrue();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return document.RootElement.Clone();
    }

    /// <summary>An extension request for a guarded event is answered with a deny while the hook has no consent, and with its answer once the user approved it.</summary>
    [Fact]
    public async Task AGuardedRequestIsDeniedUntilTheHookIsApproved()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var endpoint = session.Endpoint.ShouldNotBeNull();
        var body = "{\"event\":\"beforeShellExecution\",\"kind\":\"cursor\",\"toolCallId\":\"c1\",\"payload\":{\"command\":\"git push origin main\",\"cwd\":\"/w\",\"sandbox\":false}}";

        var denied = await PostAsync(endpoint, body, ct).ConfigureAwait(true);

        denied.GetProperty("permission").GetString().ShouldBe("deny");
        denied.GetProperty("agent_message").GetString().ShouldNotBeNull().ShouldContain("no consent", Case.Insensitive);
        rig.Runner.SpawnCount.ShouldBe(0);

        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var allowed = await PostAsync(endpoint, body, ct).ConfigureAwait(true);

        allowed.GetProperty("permission").GetString().ShouldBe("allow");
        allowed.GetProperty("hooks_run").GetInt32().ShouldBe(1);
        rig.Runner.SpawnCount.ShouldBe(1);
    }

    /// <summary>A hook's deny comes back to the extension with the messages the hook gave, the user message and the model message, and the notice names the tool call.</summary>
    [Fact]
    public async Task ADenyCarriesTheHooksMessagesAndRaisesANotice()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        rig.Runner.Behavior = static (_, _) => Task.FromResult(ScriptedHookRunner.Say("{\"permission\":\"deny\",\"user_message\":\"not today\",\"agent_message\":\"ask a human\"}"));
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var notices = new List<HookNotice>();
        session.NoticeRaised += (_, e) => notices.Add(e.Notice);

        var answer = await PostAsync(
            session.Endpoint.ShouldNotBeNull(),
            "{\"event\":\"beforeShellExecution\",\"kind\":\"cursor\",\"toolCallId\":\"c9\",\"payload\":{\"command\":\"git push\",\"cwd\":\"/w\",\"sandbox\":false}}",
            ct).ConfigureAwait(true);

        answer.GetProperty("permission").GetString().ShouldBe("deny");
        answer.GetProperty("user_message").GetString().ShouldBe("not today");
        answer.GetProperty("agent_message").GetString().ShouldBe("ask a human");
        var notice = notices.ShouldHaveSingleItem();
        notice.ToolCallId.ShouldBe("c9");
        notice.Message.ShouldBe("not today");
    }

    /// <summary>The context a sessionStart hook returned through the bridge is held for the next prompt, which the ACP gate then carries.</summary>
    [Fact]
    public async Task SessionStartContextFromTheBridgeReachesTheNextPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Json = "{\"version\":1,\"hooks\":{\"sessionStart\":[{\"command\":\"./start.sh\",\"id\":\"start\"}]}}";
        var rig = await HooksServiceRig.CreateAsync(Json, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        rig.Runner.Behavior = static (_, _) => Task.FromResult(ScriptedHookRunner.Say("{\"additional_context\":\"Use the staging database.\"}"));
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var answer = await PostAsync(
            session.Endpoint.ShouldNotBeNull(),
            "{\"event\":\"sessionStart\",\"kind\":\"cursor\",\"payload\":{\"session_id\":\"s1\",\"is_background_agent\":false}}",
            ct).ConfigureAwait(true);
        var verdict = await session.Gate.ShouldNotBeNull().BeforePromptAsync("hello", ct).ConfigureAwait(true);

        answer.GetProperty("additional_context").GetString().ShouldBe("Use the staging database.");
        verdict.PromptContext.ShouldBe("Use the staging database.");
    }

    /// <summary>A request with the wrong token never reaches the hooks.</summary>
    [Fact]
    public async Task ARequestWithTheWrongTokenNeverReachesTheHooks()
    {
        var ct = TestContext.Current.CancellationToken;
        var rig = await HooksServiceRig.CreateAsync(HooksServiceRig.DefaultHooksJson, withPlugin: true, ct).ConfigureAwait(true);
        await using var rigScope = rig.ConfigureAwait(true);
        await rig.ApproveAllAsync(ct).ConfigureAwait(true);
        var session = await rig.Service.StartSessionAsync(rig.Request(), ct).ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var endpoint = session.Endpoint.ShouldNotBeNull();
        using var handler = new HttpClientHandler { UseProxy = false, CheckCertificateRevocationList = true };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Environment[HookBridgeEnvironment.Url])
        {
            Content = new StringContent("{\"event\":\"beforeShellExecution\",\"payload\":{\"command\":\"ls\"}}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-token");

        using var response = await client.SendAsync(request, ct).ConfigureAwait(true);

        ((int)response.StatusCode).ShouldBe(401);
        rig.Runner.SpawnCount.ShouldBe(0);
    }

    /// <summary>The merged decision converts to the bridge's wire form field by field.</summary>
    [Fact]
    public void ADecisionConvertsToTheBridgeWireForm()
    {
        using var input = JsonDocument.Parse("{\"command\":\"npm ci\"}");
        var decision = new HookDecision
        {
            Verdict = HookVerdict.Ask,
            UserMessage = "check this",
            AgentMessage = "careful",
            UpdatedInput = input.RootElement.Clone(),
            AdditionalContext = "context",
            FollowupMessage = "again",
            ShouldContinue = false,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["KEY"] = "value" },
            HooksRun = 2,
        };

        var bridge = HookBridgeAdapter.ToBridge(decision);

        bridge.Permission.ShouldBe("ask");
        bridge.UserMessage.ShouldBe("check this");
        bridge.AgentMessage.ShouldBe("careful");
        bridge.UpdatedInput.ShouldNotBeNull().GetProperty("command").GetString().ShouldBe("npm ci");
        bridge.AdditionalContext.ShouldBe("context");
        bridge.FollowupMessage.ShouldBe("again");
        bridge.Continue.ShouldBe(false);
        bridge.Env.ShouldNotBeNull()["KEY"].ShouldBe("value");
        bridge.HooksRun.ShouldBe(2);
        HookBridgeAdapter.ToBridge(HookDecision.None).Permission.ShouldBeNull();
    }
}
