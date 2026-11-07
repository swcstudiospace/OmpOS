using System.Text;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Dispatch.Desktop.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Desktop.Tests;

public sealed class PolicyTests : IDisposable
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_710_000_000);

    private static readonly DispatchComputerUseConfig AllComputerUseKeys = new(ComputerUseEnabled: true, AllowDispatchSessions: true);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dispatch-policy-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void TheDefaultsKeepEveryCapabilityOffExceptSessionControl()
    {
        var defaults = new DispatchPolicy();

        defaults.CodeEnabled.ShouldBeFalse();
        defaults.CoworkEnabled.ShouldBeFalse();
        defaults.BrowserUseEnabled.ShouldBeFalse();
        defaults.ComputerUseOptIn.ShouldBeFalse();
        defaults.Paused.ShouldBeFalse();

        Evaluate(DispatchOp.SessionList, DispatchCapability.Sessions, "{}", defaults).ShouldBe(DispatchPolicyDecision.Allow());
        Evaluate(DispatchOp.CodePrompt, DispatchCapability.Code, "{}", defaults).Reason.ShouldBe(DispatchFailureReason.CapabilityDisabled);
        Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, "{}", defaults).Reason.ShouldBe(DispatchFailureReason.CapabilityDisabled);
    }

    [Fact]
    public void ApprovalDecideIsRefusedForKindComputer()
    {
        var policy = new DispatchPolicy { CoworkEnabled = true, ComputerUseOptIn = true };

        var decision = Evaluate(DispatchOp.ApprovalDecide, DispatchCapability.Sessions, "{\"kind\":\"computer\"}", policy, AllComputerUseKeys);

        decision.Allowed.ShouldBeFalse();
        decision.Reason.ShouldBe(DispatchFailureReason.PolicyDenied);
    }

    [Fact]
    public void ACodePromptNeverRegistersAComputerToolAndRefusesToolClasses()
    {
        var policy = new DispatchPolicy { CodeEnabled = true, ComputerUseOptIn = true };

        Evaluate(DispatchOp.CodePrompt, DispatchCapability.Code, "{\"text\":\"fix it\"}", policy, AllComputerUseKeys).Allowed.ShouldBeTrue();
        Evaluate(DispatchOp.CodePrompt, DispatchCapability.Code, "{\"toolClasses\":[\"computer_use\"]}", policy, AllComputerUseKeys)
            .Reason.ShouldBe(DispatchFailureReason.PayloadInvalid);
    }

    [Fact]
    public void AComputerUseTaskNeedsAllThreeGates()
    {
        var payload = "{\"toolClasses\":[\"computer_use\"],\"text\":\"open the browser\"}";
        var complete = new DispatchPolicy { CoworkEnabled = true, ComputerUseOptIn = true };

        var allowed = Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, payload, complete, AllComputerUseKeys);
        allowed.Allowed.ShouldBeTrue();
        allowed.Task!.ToolClasses.ShouldContain(DispatchTaskPayload.ComputerUse, StringComparer.Ordinal);

        Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, payload, new DispatchPolicy { CoworkEnabled = true }, AllComputerUseKeys)
            .Reason.ShouldBe(DispatchFailureReason.CapabilityDisabled);
        Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, payload, complete, new DispatchComputerUseConfig(true, false))
            .Reason.ShouldBe(DispatchFailureReason.CapabilityDisabled);
        Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, payload, complete, new DispatchComputerUseConfig(false, true))
            .Reason.ShouldBe(DispatchFailureReason.CapabilityDisabled);
    }

    [Fact]
    public void ABrowserUseTaskNeedsTheBrowserGate()
    {
        var payload = "{\"toolClasses\":[\"browser_use\"]}";

        Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, payload, new DispatchPolicy { CoworkEnabled = true })
            .Reason.ShouldBe(DispatchFailureReason.CapabilityDisabled);
        Evaluate(DispatchOp.CoworkTask, DispatchCapability.Cowork, payload, new DispatchPolicy { CoworkEnabled = true, BrowserUseEnabled = true })
            .Allowed.ShouldBeTrue();
    }

    [Fact]
    public void APausedDesktopStartsNoNewWorkFromAController()
    {
        var paused = new DispatchPolicy { CodeEnabled = true, Paused = true };

        Evaluate(DispatchOp.CodePrompt, DispatchCapability.Code, "{}", paused).Reason.ShouldBe(DispatchFailureReason.PolicyDenied);
        Evaluate(DispatchOp.SessionList, DispatchCapability.Sessions, "{}", paused).Allowed.ShouldBeTrue();
    }

    [Fact]
    public void AnOperationOutsideItsCapabilityIsAPayloadError()
    {
        var policy = new DispatchPolicy { CodeEnabled = true };

        Evaluate(DispatchOp.CodePrompt, DispatchCapability.Sessions, "{}", policy).Reason.ShouldBe(DispatchFailureReason.PayloadInvalid);
    }

    [Fact]
    public void TheApprovalShowsOnlyTheFirst200CharactersAsPlainText()
    {
        var longText = new string('a', 500);
        var payload = DispatchTaskPayload.TryParse(Encoding.UTF8.GetBytes("{\"text\":\"" + longText + "\"}"), out var task);

        payload.ShouldBeTrue();
        task.ApprovalText().Length.ShouldBe(DispatchTaskPayload.MaxApprovalTextChars);
    }

    [Fact]
    public async Task ApproveOnceGrantsTheTaskForThirtyMinutes()
    {
        var clock = new ManualTimeProvider(Start);
        var flow = new DispatchApprovalFlow(new FixedChoicePrompt(DispatchApprovalChoice.ApproveOnce), clock);

        var result = await flow.ApproveTaskAsync(Request(), CancellationToken.None);

        result.Approved.ShouldBeTrue();
        result.PauseRequested.ShouldBeFalse();
        flow.Grant!.ExpiresAt.ShouldBe(Start + TimeSpan.FromMinutes(30));

        flow.Withdraw();
        flow.Grant.ShouldBeNull();
    }

    [Fact]
    public async Task DenyAndPauseDeniesAndAsksForAPause()
    {
        var flow = new DispatchApprovalFlow(new FixedChoicePrompt(DispatchApprovalChoice.DenyAndPause), new ManualTimeProvider(Start));

        var result = await flow.ApproveTaskAsync(Request(), CancellationToken.None);

        result.Approved.ShouldBeFalse();
        result.Reason.ShouldBe(DispatchFailureReason.ApprovalDenied);
        result.PauseRequested.ShouldBeTrue();
        flow.Grant.ShouldBeNull();
    }

    [Fact]
    public async Task ANoAnswerInTimeIsADenialWithTheTimeoutReason()
    {
        var flow = new DispatchApprovalFlow(new NeverAnsweringPrompt(), TimeProvider.System, TimeSpan.FromMilliseconds(50));

        var result = await flow.ApproveTaskAsync(Request(), CancellationToken.None);

        result.Approved.ShouldBeFalse();
        result.Reason.ShouldBe(DispatchFailureReason.ApprovalTimeout);
    }

    [Fact]
    public void ThePolicyPersistsAndAReadableFileIsLoadedOnRestart()
    {
        var path = Path.Combine(_directory, "policy.json");
        var store = new DispatchPolicyStore(path);
        store.Current.CodeEnabled.ShouldBeFalse();

        store.Save(new DispatchPolicy { CodeEnabled = true, Paused = true });

        var restarted = new DispatchPolicyStore(path);
        restarted.Current.CodeEnabled.ShouldBeTrue();
        restarted.Current.Paused.ShouldBeTrue();
    }

    private static DispatchApprovalRequest Request() => new("Controller", [DispatchTaskPayload.ComputerUse], "task");

    private static DispatchPolicyDecision Evaluate(
        DispatchOp op,
        DispatchCapability capability,
        string payloadJson,
        DispatchPolicy policy,
        DispatchComputerUseConfig? computerUse = null)
    {
        var command = new DispatchCommand("cmd-1", capability, op, Encoding.UTF8.GetBytes(payloadJson));
        return DispatchPolicyEvaluator.Evaluate(command, policy, computerUse ?? default);
    }

    private sealed class FixedChoicePrompt(DispatchApprovalChoice choice) : IDispatchApprovalPrompt
    {
        public ValueTask<DispatchApprovalChoice> RequestAsync(DispatchApprovalRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(choice);
    }

    private sealed class NeverAnsweringPrompt : IDispatchApprovalPrompt
    {
        public async ValueTask<DispatchApprovalChoice> RequestAsync(DispatchApprovalRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return DispatchApprovalChoice.Deny;
        }
    }
}
