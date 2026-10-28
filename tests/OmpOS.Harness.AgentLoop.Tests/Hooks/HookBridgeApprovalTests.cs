using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Tests.Stages;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies that the server resolves an <c>ask</c> decision itself before it answers: approved becomes allow, declined, failed
/// and timed out become deny, no prompt is deny, and a decision that is not <c>ask</c> never reaches the prompt. The extension
/// therefore never sees <c>ask</c>.
/// </summary>
public sealed class HookBridgeApprovalTests
{
    private const string Request = "{\"event\":\"beforeShellExecution\",\"payload\":{\"command\":\"rm -rf build\"}}";
    private static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Safety = TimeSpan.FromSeconds(30);

    private static FakeBridgeHandler Asking(string permission = BridgeDecision.PermissionAsk) => FakeBridgeHandler.Returning(new BridgeDecision
    {
        Permission = permission,
        UserMessage = "Run rm -rf build?",
        AgentMessage = "waiting for the user",
        AdditionalContext = "context from the hook",
        HooksRun = 2,
    });

    [Fact]
    public async Task AnApprovedAskBecomesAllowAndKeepsTheRestOfTheDecision()
    {
        var prompt = FakeApprovalPrompt.Answering(approved: true);
        var rig = await HookBridgeRig.StartAsync(Asking(), prompt: prompt).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Request).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var json = response.Json();
        json.GetProperty("permission").GetString().ShouldBe("allow");
        json.GetProperty("user_message").GetString().ShouldBe("Run rm -rf build?");
        json.GetProperty("additional_context").GetString().ShouldBe("context from the hook");
        json.GetProperty("hooks_run").GetInt32().ShouldBe(2);
        prompt.Calls.ShouldBe(1);
        var askedRequest = prompt.LastRequest.ShouldNotBeNull();
        askedRequest.Event.ShouldBe("beforeShellExecution");
        askedRequest.Payload.GetProperty("command").GetString().ShouldBe("rm -rf build");
        var askedDecision = prompt.LastDecision.ShouldNotBeNull();
        askedDecision.Permission.ShouldBe("ask");
        askedDecision.UserMessage.ShouldBe("Run rm -rf build?");
    }

    [Fact]
    public async Task ADeclinedAskBecomesDenyWithAMessageForTheModelAndTheHumanMessageKept()
    {
        var prompt = FakeApprovalPrompt.Answering(approved: false);
        var rig = await HookBridgeRig.StartAsync(Asking(), prompt: prompt).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Request).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var json = response.Json();
        json.GetProperty("permission").GetString().ShouldBe("deny");
        json.GetProperty("agent_message").GetString().ShouldNotBeNullOrWhiteSpace();
        json.GetProperty("agent_message").GetString().ShouldNotBe("waiting for the user", StringComparer.Ordinal);
        json.GetProperty("user_message").GetString().ShouldBe("Run rm -rf build?");
        prompt.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AnAskWithoutAnApprovalPromptIsDeniedWithTheDocumentedMessage()
    {
        var rig = await HookBridgeRig.StartAsync(Asking()).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Request).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var json = response.Json();
        json.GetProperty("permission").GetString().ShouldBe("deny");
        json.GetProperty("agent_message").GetString().ShouldBe("approval required but no approval prompt is available");
        json.GetProperty("user_message").GetString().ShouldBe("Run rm -rf build?");
    }

    [Fact]
    public async Task APromptThatThrowsDeniesTheActionAndTheExceptionTextStaysOutOfTheAnswerAndTheLog()
    {
        var logger = new CapturingLogger();
        var prompt = FakeApprovalPrompt.Throwing(new InvalidOperationException("TOPSECRET-DIALOG-TEXT"));
        var rig = await HookBridgeRig.StartAsync(Asking(), prompt: prompt, logger: logger).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Request).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        response.Json().GetProperty("permission").GetString().ShouldBe("deny");
        response.Json().GetProperty("agent_message").GetString().ShouldNotBeNullOrWhiteSpace();
        response.Body.ShouldNotContain("TOPSECRET", Case.Sensitive);
        await rig.DisposeAsync().ConfigureAwait(true);
        logger.Messages.ShouldAllBe(static message => !message.Contains("TOPSECRET", StringComparison.Ordinal));
        logger.Messages.ShouldContain(static message => message.Contains("approval prompt failed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("allow")]
    [InlineData("deny")]
    [InlineData("something-else")]
    [InlineData(null)]
    public async Task OnlyAnAskDecisionReachesThePromptAndEveryOtherPermissionPassesThroughUnchanged(string? permission)
    {
        var prompt = FakeApprovalPrompt.Answering(approved: true);
        var rig = await HookBridgeRig.StartAsync(Asking(permission!), prompt: prompt).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Request).ConfigureAwait(true);

        response.Status.ShouldBe(200);
        prompt.Calls.ShouldBe(0);
        if (permission is null)
        {
            response.PropertyNames().ShouldNotContain("permission", StringComparer.Ordinal);
        }
        else
        {
            response.Json().GetProperty("permission").GetString().ShouldBe(permission);
        }
    }

    [Theory]
    [InlineData("ASK")]
    [InlineData("Ask")]
    public async Task TheAskPermissionIsRecognisedWithoutRegardToCase(string permission)
    {
        var prompt = FakeApprovalPrompt.Answering(approved: true);
        var rig = await HookBridgeRig.StartAsync(Asking(permission), prompt: prompt).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(Request).ConfigureAwait(true);

        response.Json().GetProperty("permission").GetString().ShouldBe("allow");
        prompt.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task APromptThatRunsPastTheTimeoutIsCancelledAndTheActionIsDenied()
    {
        var clock = new StageFakeTimeProvider();
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompt = FakeApprovalPrompt.Running(async (_, _, cancellationToken) =>
        {
            asked.TrySetResult();
            try
            {
                await never.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            return true;
        });
        var options = new HookBridgeOptions { HandlerTimeout = HandlerTimeout };
        var rig = await HookBridgeRig.StartAsync(Asking(), options, prompt, clock).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync(Request);

        await asked.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        clock.Advance(HandlerTimeout);
        var response = await pending.ConfigureAwait(true);

        await cancelled.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.Status.ShouldBe(200);
        response.Json().GetProperty("permission").GetString().ShouldBe("deny");
        response.Json().GetProperty("agent_message").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ThePromptIsAskedOncePerAskDecisionWhenRequestsRunTogether()
    {
        var prompt = FakeApprovalPrompt.Answering(approved: true);
        var rig = await HookBridgeRig.StartAsync(Asking(), prompt: prompt).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => rig.PostAsync(Request))).ConfigureAwait(true);

        responses.ShouldAllBe(static response => response.Status == 200);
        prompt.Calls.ShouldBe(8);
    }
}
