using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Harness.Automations.Tests;
using Autonogrammer.Native.Composition.Automations;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.Automations;
using Autonogrammer.Presentation.Cloud;
using Shouldly;
using Xunit;
using ViewDraft = Autonogrammer.Presentation.Automations.AutomationDraft;

namespace Autonogrammer.Native.Tests.Composition.Automations;

/// <summary>
/// The automations of the desktop, end to end below the window: a definition saved through the manager lands in a file, the scheduler the host runs fires it on the fake clock, a new session is started
/// with the prompt through the head's sessions, and the history records the run with the session. The sessions are scripted and the clock is advanced by hand: no sleeps, no omp.
/// </summary>
public sealed class AutomationsHostTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ViewDraft LocalDraft(string prompt = AutomationFixtures.Prompt, string cron = "* * * * *") => new(
        "Morning summary",
        prompt,
        Enabled: true,
        "UTC",
        [new AutomationTriggerDraft("cron", cron, string.Empty, [], [], [])],
        "local",
        [],
        [],
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        MaxConcurrentRuns: 1,
        MaxRunsPerHour: 12,
        MaxRuntimeMinutes: 30);

    private static ViewDraft CloudDraft() => LocalDraft() with
    {
        TargetKind = "cloud",
        Repositories = [new AutomationRepositoryDraft("https://github.com/acme/app", "main", string.Empty)],
        SecretRefs = ["DEPLOY_TOKEN"],
        Model = "model-x",
    };

    private static AutomationsHost Host(TempTree tree, FakeTimeProvider clock, bool localAvailable = true, ICloudAgentsHost? cloud = null) =>
        new(Path.Combine(tree.Root, "automations.json"), Path.Combine(tree.Root, "consent.json"), cloud, () => tree.Workspace, localAvailable, clock);

    private static async Task<IReadOnlyList<AutomationRunEntry>> HistoryWhenAsync(AutomationsHost host, string id, Func<IReadOnlyList<AutomationRunEntry>, bool> done)
    {
        IReadOnlyList<AutomationRunEntry> history = [];
        await AutomationFixtures.EventuallyAsync(
            async () =>
            {
                history = await host.Store.GetHistoryAsync(id, Token).ConfigureAwait(true);
                return done(history);
            },
            "the run history reaches the expected state",
            Token).ConfigureAwait(true);
        return history;
    }

    /// <summary>A saved definition fires on the clock: a new session starts with the prompt in the workspace, the first run was confirmed, the second was not, and the history holds both with their sessions.</summary>
    [Fact]
    public async Task ASavedDefinitionFiresOnTheClockAndRecordsItsSession()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var sessions = new ScriptedAutomationSessions();
        var host = Host(tree, clock);
        await using var hostScope = host.ConfigureAwait(true);
        host.Attach(sessions);
        var saved = await host.Manager.SaveAsync(null, LocalDraft(), Token).ConfigureAwait(true);
        host.Start();

        clock.Advance(TimeSpan.FromMinutes(1));
        var first = await HistoryWhenAsync(host, saved.Entry.Id, static history => history.Any(static run => run.Status == AutomationRunStatus.Succeeded)).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromMinutes(1));
        var both = await HistoryWhenAsync(host, saved.Entry.Id, static history => history.Count(static run => run.Status == AutomationRunStatus.Succeeded) == 2).ConfigureAwait(true);

        first.Single(static run => run.Status == AutomationRunStatus.Succeeded).Source.ShouldBe(AutomationRunSource.Cron);
        both.Select(static run => run.ExternalId).Order(StringComparer.Ordinal).ShouldBe(["session-1", "session-2"]);
        sessions.Created[0].WorkspaceRoot.ShouldBe(tree.Workspace);
        sessions.Created[0].Calls.Count.ShouldBe(2);
        sessions.Created[0].Calls[0].ShouldBe("confirm");
        sessions.Created[0].Calls[1].ShouldStartWith("run:", Case.Sensitive);
        sessions.Created[0].Calls[1].ShouldContain(AutomationFixtures.Prompt, Case.Sensitive);
        sessions.Created[1].Calls.ShouldHaveSingleItem().ShouldStartWith("run:", Case.Sensitive);
        host.IsStarted.ShouldBeTrue();
    }

    /// <summary>A disabled automation does not fire, however long the clock runs.</summary>
    [Fact]
    public async Task ADisabledAutomationDoesNotFire()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var sessions = new ScriptedAutomationSessions();
        var host = Host(tree, clock);
        await using var hostScope = host.ConfigureAwait(true);
        host.Attach(sessions);
        var saved = await host.Manager.SaveAsync(null, LocalDraft() with { Enabled = false }, Token).ConfigureAwait(true);
        host.Start();

        clock.Advance(TimeSpan.FromMinutes(3));
        _ = await host.Scheduler.TickAsync(Token).ConfigureAwait(true);

        sessions.Created.ShouldBeEmpty();
        (await host.Store.GetHistoryAsync(saved.Entry.Id, Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>A head without omp has no local target: the page says so, a local automation cannot be saved, and a stored one that fires fails with a sentence instead of starting anything.</summary>
    [Fact]
    public async Task WithoutOmpLocalAutomationsAreDisabled()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var sessions = new ScriptedAutomationSessions();
        var host = Host(tree, clock, localAvailable: false);
        await using var hostScope = host.ConfigureAwait(true);
        host.Attach(sessions);

        host.Manager.SupportedTargetKinds.ShouldBeEmpty();
        host.Manager.PageNote.ShouldBe(AutomationsCopy.NoOmpNote);
        host.Runner.HasTarget(AutomationTargetKind.Local).ShouldBeFalse();
        var refused = await Should.ThrowAsync<AutomationsSourceException>(() => host.Manager.SaveAsync(null, LocalDraft(), Token)).ConfigureAwait(true);
        refused.Message.ShouldBe(AutomationsCopy.NoOmpNote);

        await host.Store.SaveAsync(AutomationFixtures.Local(Start), Token).ConfigureAwait(true);
        host.Start();
        clock.Advance(TimeSpan.FromMinutes(1));
        var history = await HistoryWhenAsync(host, "auto-1", static history => history.Any(static run => run.Status == AutomationRunStatus.Failed)).ConfigureAwait(true);

        history.First(static run => run.Status == AutomationRunStatus.Failed).Error.ShouldBe("No Local target is available on this host.");
        sessions.Created.ShouldBeEmpty();
    }

    /// <summary>A cloud automation goes to the cloud agents host: the agent is created with the repositories, secret names and model of the definition, receives the prompt, and the run records its identifier.</summary>
    [Fact]
    public async Task ACloudAutomationLaunchesAnAgentThroughTheCloudHost()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var cloud = new FakeCloudAgentsHost(clock);
        var host = Host(tree, clock, localAvailable: false, cloud: cloud);
        await using var hostScope = host.ConfigureAwait(true);
        host.Manager.SupportedTargetKinds.ShouldBe(["cloud"]);
        var saved = await host.Manager.SaveAsync(null, CloudDraft(), Token).ConfigureAwait(true);

        var run = await host.Manager.RunNowAsync(saved.Entry.Id, Token).ConfigureAwait(true);
        var history = await HistoryWhenAsync(host, saved.Entry.Id, static history => history.Any(static entry => entry.Status == AutomationRunStatus.Launched)).ConfigureAwait(true);

        run.Trigger.ShouldBe("manual");
        history.Single().ExternalId.ShouldBe("cloud-1");
        var request = cloud.CreateRequests.Single();
        request.Repositories.Select(static repository => repository.Address).ShouldBe(["https://github.com/acme/app"]);
        request.Branch.ShouldBe("main");
        request.SecretNames.ShouldBe(["DEPLOY_TOKEN"]);
        request.Model.ShouldBe("model-x");
        cloud.Prompts.Single().ShouldContain(AutomationFixtures.Prompt, Case.Sensitive);
    }

    /// <summary>A cloud run whose first task cannot be sent is a failed run that still names the agent, so the user can find and delete it.</summary>
    [Fact]
    public async Task ACloudRunWhoseTaskIsRefusedFailsAndNamesTheAgent()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var cloud = new FakeCloudAgentsHost(clock);
        cloud.FailNext("Prompt", new CloudHostException(CloudFailureKind.Unavailable, "refused"));
        var host = Host(tree, clock, localAvailable: false, cloud: cloud);
        await using var hostScope = host.ConfigureAwait(true);
        var saved = await host.Manager.SaveAsync(null, CloudDraft(), Token).ConfigureAwait(true);

        _ = await host.Manager.RunNowAsync(saved.Entry.Id, Token).ConfigureAwait(true);
        var history = await HistoryWhenAsync(host, saved.Entry.Id, static history => history.Any(static entry => entry.Status == AutomationRunStatus.Failed)).ConfigureAwait(true);

        history.Single().ExternalId.ShouldBe("cloud-1");
        history.Single().Error.ShouldBe("The cloud agent was created but its first task could not be sent.");
    }

    /// <summary>Disposing the host stops the scheduler: a later tick of the clock starts nothing.</summary>
    [Fact]
    public async Task DisposingTheHostStopsTheScheduler()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var sessions = new ScriptedAutomationSessions();
        var host = Host(tree, clock);
        host.Attach(sessions);
        _ = await host.Manager.SaveAsync(null, LocalDraft(), Token).ConfigureAwait(true);
        host.Start();

        await host.DisposeAsync().ConfigureAwait(true);
        clock.Advance(TimeSpan.FromMinutes(5));

        sessions.Created.ShouldBeEmpty();
    }
}
