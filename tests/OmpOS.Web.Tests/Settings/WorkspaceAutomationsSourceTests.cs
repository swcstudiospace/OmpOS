using System.Net;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Automations;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests.Settings;

/// <summary>The Automations page over <c>GET /automations</c>: a read-only list that can neither enable nor resume an automation.</summary>
public sealed class WorkspaceAutomationsSourceTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AutomationsResponse Available(params WorkspaceAutomationInfo[] automations) => new() { Available = true, Automations = automations };

    /// <summary>Automations map with their trigger, flag, longest run, last run outcome and pause.</summary>
    [Theory]
    [InlineData("succeeded", AutomationOutcome.Succeeded)]
    [InlineData("failed", AutomationOutcome.Failed)]
    [InlineData("timed-out", AutomationOutcome.TimedOut)]
    [InlineData("cancelled", AutomationOutcome.Cancelled)]
    [InlineData("blocked", AutomationOutcome.Blocked)]
    [InlineData("refused", AutomationOutcome.Refused)]
    public async Task Automations_MapEveryOutcome(string wire, AutomationOutcome expected)
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = Available(
            new WorkspaceAutomationInfo
            {
                Id = "nightly",
                Trigger = "schedule",
                Enabled = true,
                MaxRuntimeSeconds = 900,
                LastRun = new AutomationLastRunInfo { Outcome = wire, Reason = "why", RecordedAt = Stamp },
                Pause = new AutomationPauseInfo { Guard = "loop", Reason = "Three failures.", HowToResume = "Resume it." },
            },
            new WorkspaceAutomationInfo { Id = "manual", Trigger = "manual", Enabled = false, MaxRuntimeSeconds = 60 });
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.AutomationsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceAutomationsSource(WorkspaceFixtures.Client(http));

        await source.LoadAsync(Token).ConfigureAwait(true);
        var entries = source.Snapshot();

        entries.Select(static entry => entry.Id).ShouldBe(["nightly", "manual"]);
        entries[0].Trigger.ShouldBe("schedule");
        entries[0].IsEnabled.ShouldBeTrue();
        entries[0].MaxRuntime.ShouldBe(TimeSpan.FromMinutes(15));
        entries[0].LastRun.ShouldNotBeNull().Outcome.ShouldBe(expected);
        entries[0].LastRun!.RecordedAt.ShouldBe(Stamp);
        entries[0].Pause.ShouldNotBeNull().Guard.ShouldBe("loop");
        entries[1].LastRun.ShouldBeNull();
        entries[1].Pause.ShouldBeNull();
    }

    /// <summary>An outcome this page does not know is a load failure, not a silently wrong row.</summary>
    [Fact]
    public async Task AnUnknownOutcome_FailsTheLoad()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = Available(new WorkspaceAutomationInfo
        {
            Id = "x",
            Trigger = "manual",
            Enabled = true,
            MaxRuntimeSeconds = 1,
            LastRun = new AutomationLastRunInfo { Outcome = "exploded", Reason = string.Empty, RecordedAt = Stamp },
        });
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.AutomationsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceAutomationsSource(WorkspaceFixtures.Client(http));

        _ = await Should.ThrowAsync<AutomationsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);
    }

    /// <summary>An API with no automation engine is the page's error state with the API's reason.</summary>
    [Fact]
    public async Task NoEngine_IsTheErrorStateWithTheReason()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = new AutomationsResponse { Available = false, Reason = "No automation engine is configured.", Automations = [] };
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.AutomationsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceAutomationsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<AutomationsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("No automation engine is configured.");
        source.Snapshot().ShouldBeEmpty();
    }

    /// <summary>A refused call is a load failure.</summary>
    [Fact]
    public async Task ARefusedCall_FailsTheLoad()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var refused = WorkspaceFixtures.Text(HttpStatusCode.NotFound, string.Empty);
        handler.Enqueue(refused);
        var source = new WorkspaceAutomationsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<AutomationsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("not available to your account", Case.Sensitive);
    }

    /// <summary>Enabling and resuming are refused with the read-only sentence, and no request is sent for them.</summary>
    [Fact]
    public async Task EveryChange_IsRefused_AndSendsNothing()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var source = new WorkspaceAutomationsSource(WorkspaceFixtures.Client(http));

        var enable = await Should.ThrowAsync<AutomationsSourceException>(() => source.SetEnabledAsync("a", true, Token)).ConfigureAwait(true);
        var resume = await Should.ThrowAsync<AutomationsSourceException>(() => source.ResumeAsync("a", Token)).ConfigureAwait(true);

        new[] { enable.Message, resume.Message }.ShouldAllBe(static message => message == WorkspaceAutomationsSource.ReadOnlyMessage);
        handler.Requests.ShouldBeEmpty();
    }
}
