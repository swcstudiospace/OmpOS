using OmpOS.Harness.Automations.Hosted;
using OmpOS.Native.Composition.Automations;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Presentation.Automations;
using OmpOS.Workspace;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition.Automations;

/// <summary>
/// What a local automation run does in the head: it starts a new session, asks for the confirmation of the first run only (and again when the prompt changes), sends the prompt as the first turn,
/// and the run ends with the turn. The sessions are scripted; nothing starts omp.
/// </summary>
public sealed class LocalSessionAutomationTargetTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LocalSessionAutomationTarget Target(ScriptedAutomationSessions? sessions, FileSetupConsentStore consent, string workspace = "/work/space") =>
        new(() => sessions, () => workspace, consent);

    /// <summary>The first run confirms, sends the prompt in a session titled with the automation's name in the workspace, and records the session in the result; the second run asks nothing.</summary>
    [Fact]
    public async Task TheFirstRunAsksOnceAndLaterRunsGoStraightToThePrompt()
    {
        using var tree = new TempTree();
        using var consent = new FileSetupConsentStore(Path.Combine(tree.Root, "consent.json"));
        var sessions = new ScriptedAutomationSessions();
        var target = Target(sessions, consent);
        var automation = AutomationFixtures.Local(Start);

        var first = await target.RunAsync(AutomationFixtures.Request(automation), Token).ConfigureAwait(true);
        var second = await target.RunAsync(AutomationFixtures.Request(automation), Token).ConfigureAwait(true);

        first.Status.ShouldBe(AutomationRunStatus.Succeeded);
        first.ExternalId.ShouldBe("session-1");
        second.ExternalId.ShouldBe("session-2");
        sessions.Created[0].Calls.ShouldBe(["confirm", "run:" + AutomationFixtures.Prompt]);
        sessions.Created[0].Title.ShouldBe("Morning summary");
        sessions.Created[0].WorkspaceRoot.ShouldBe("/work/space");
        sessions.Created[0].Confirmation!.Title.ShouldBe(AutomationsCopy.ConsentCardTitle);
        sessions.Created[0].Confirmation!.Summary.ShouldContain("Morning summary", Case.Sensitive);
        sessions.Created[1].Calls.ShouldBe(["run:" + AutomationFixtures.Prompt]);
    }

    /// <summary>Editing the prompt (or moving the automation to another workspace) is a different automation as far as the consent goes: it asks again.</summary>
    [Fact]
    public async Task AChangedPromptAsksAgain()
    {
        using var tree = new TempTree();
        using var consent = new FileSetupConsentStore(Path.Combine(tree.Root, "consent.json"));
        var sessions = new ScriptedAutomationSessions();
        var target = Target(sessions, consent);

        _ = await target.RunAsync(AutomationFixtures.Request(AutomationFixtures.Local(Start)), Token).ConfigureAwait(true);
        _ = await target.RunAsync(AutomationFixtures.Request(AutomationFixtures.Local(Start, prompt: "Delete the stale branches.")), Token).ConfigureAwait(true);

        sessions.Created[1].Calls.ShouldBe(["confirm", "run:Delete the stale branches."]);
    }

    /// <summary>A consent given in one run is still there in a new process: the record is in a file.</summary>
    [Fact]
    public async Task TheConsentSurvivesARestart()
    {
        using var tree = new TempTree();
        var path = Path.Combine(tree.Root, "consent.json");
        var sessions = new ScriptedAutomationSessions();
        var automation = AutomationFixtures.Local(Start);
        using (var consent = new FileSetupConsentStore(path))
        {
            _ = await Target(sessions, consent).RunAsync(AutomationFixtures.Request(automation), Token).ConfigureAwait(true);
        }

        using var reopened = new FileSetupConsentStore(path);
        _ = await Target(sessions, reopened).RunAsync(AutomationFixtures.Request(automation), Token).ConfigureAwait(true);

        sessions.Created[1].Calls.ShouldBe(["run:" + AutomationFixtures.Prompt]);
    }

    /// <summary>A denied confirmation sends nothing: the run fails with the session recorded, the session is closed, and the next run asks again.</summary>
    [Fact]
    public async Task ADeniedConfirmationSendsNothingAndIsAskedAgain()
    {
        using var tree = new TempTree();
        using var consent = new FileSetupConsentStore(Path.Combine(tree.Root, "consent.json"));
        var sessions = new ScriptedAutomationSessions { Allow = false };
        var target = Target(sessions, consent);
        var automation = AutomationFixtures.Local(Start);

        var denied = await target.RunAsync(AutomationFixtures.Request(automation), Token).ConfigureAwait(true);
        sessions.Allow = true;
        var allowed = await target.RunAsync(AutomationFixtures.Request(automation), Token).ConfigureAwait(true);

        denied.Status.ShouldBe(AutomationRunStatus.Failed);
        denied.Error.ShouldBe(AutomationsCopy.NotConfirmed);
        denied.ExternalId.ShouldBe("session-1");
        sessions.Created[0].Calls.ShouldBe(["confirm", "close"]);
        allowed.Status.ShouldBe(AutomationRunStatus.Succeeded);
        sessions.Created[1].Calls.ShouldBe(["confirm", "run:" + AutomationFixtures.Prompt]);
    }

    /// <summary>A turn that ends with an error fails the run and keeps the session's identifier in the result.</summary>
    [Fact]
    public async Task AFailedTurnFailsTheRunWithItsSession()
    {
        using var tree = new TempTree();
        using var consent = new FileSetupConsentStore(Path.Combine(tree.Root, "consent.json"));
        var sessions = new ScriptedAutomationSessions { Turn = AutomationSessionTurn.Failed("The agent's turn ended with an error.") };

        var result = await Target(sessions, consent).RunAsync(AutomationFixtures.Request(AutomationFixtures.Local(Start)), Token).ConfigureAwait(true);

        result.Status.ShouldBe(AutomationRunStatus.Failed);
        result.ExternalId.ShouldBe("session-1");
        result.Error.ShouldBe("The agent's turn ended with an error.");
    }

    /// <summary>A run that is cancelled (its maximum runtime passed, or the head closes) closes its session and lets the cancellation through, so the runner records TimedOut or Cancelled.</summary>
    [Fact]
    public async Task ACancelledRunClosesItsSession()
    {
        using var tree = new TempTree();
        using var consent = new FileSetupConsentStore(Path.Combine(tree.Root, "consent.json"));
        var gate = new TaskCompletionSource();
        var sessions = new ScriptedAutomationSessions { TurnGate = gate };
        var target = Target(sessions, consent);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var thrown = Should.ThrowAsync<OperationCanceledException>(() => target.RunAsync(AutomationFixtures.Request(AutomationFixtures.Local(Start)), cancellation.Token));
        await AgentLoopFixtures.EventuallyAsync(() => sessions.Created.Count > 0 && sessions.Created[0].Calls.Count == 2, "the turn started and waits on its gate", Token).ConfigureAwait(true);
        await cancellation.CancelAsync().ConfigureAwait(true);

        _ = await thrown.ConfigureAwait(true);

        sessions.Created[0].Calls.ShouldBe(["confirm", "run:" + AutomationFixtures.Prompt, "close"]);
    }

    /// <summary>A run that fires before the head opened its sessions fails with a sentence instead of throwing.</summary>
    [Fact]
    public async Task ARunBeforeTheSessionsExistFailsWithASentence()
    {
        using var tree = new TempTree();
        using var consent = new FileSetupConsentStore(Path.Combine(tree.Root, "consent.json"));

        var result = await Target(sessions: null, consent).RunAsync(AutomationFixtures.Request(AutomationFixtures.Local(Start)), Token).ConfigureAwait(true);

        result.Status.ShouldBe(AutomationRunStatus.Failed);
        result.Error.ShouldBe(AutomationsCopy.SessionsNotReady);
    }
}
