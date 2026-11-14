using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Harness.Automations.Scheduling;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The scheduled Cowork starter over the real host, and the runner in front of it. No live omp is started.</summary>
public sealed class ScheduledCoworkSessionStarterTests
{
    [Fact]
    public async Task ScheduledRunOpensACoworkSessionAndSendsItsPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var starter = new ScheduledCoworkSessionStarter(host);
                var result = await starter.StartAsync(ScheduledSessionSurface.Cowork, null, [], SessionApprovalMode.Manual, null, "Summarise the inbox", ct).ConfigureAwait(true);
                result.Error.ShouldBeNull();
                result.Started.ShouldBeTrue();
                var id = result.SessionId.ShouldNotBeNull();
                host.OriginsOf(id).ShouldBe([CoworkTurnOrigin.Local]);
                omp.Launches.Count.ShouldBe(1);
                await AgentLoopFixtures.EventuallyAsync(() => omp.Requests("session/prompt").Count == 1, "the scheduled prompt reaches omp", ct).ConfigureAwait(true);
                omp.Requests("session/prompt").ShouldHaveSingleItem()["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldBe("Summarise the inbox");
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task AScheduledRunInAProjectLinksItsTaskAndTrustsItsFolders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        using var outside = new TempDir();
        var projectFolder = Directory.CreateDirectory(System.IO.Path.Combine(root.Path, "project")).FullName;
        var runFolder = outside.Path;
        new Autonogrammer.Cowork.Projects.CoworkProjectStore(root.Path, "acct").Upsert(Project("p-1", projectFolder));
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var starter = new ScheduledCoworkSessionStarter(host);
                var result = await starter.StartAsync(ScheduledSessionSurface.Cowork, "p-1", [runFolder], SessionApprovalMode.Auto, null, "Summarise the inbox", ct).ConfigureAwait(true);
                result.Error.ShouldBeNull();
                result.Started.ShouldBeTrue();
                var id = result.SessionId.ShouldNotBeNull();
                var meta = new CoworkSessionMetaStore(root.Path, "acct").Get(id).ShouldNotBeNull();
                meta.ProjectId.ShouldBe("p-1");
                meta.MemoryOff.ShouldBeFalse();
                meta.TrustedFolders.ShouldContain(projectFolder, StringComparer.Ordinal);
                meta.TrustedFolders.ShouldContain(runFolder, StringComparer.Ordinal);
                await AgentLoopFixtures.EventuallyAsync(() => omp.Requests("session/prompt").Count == 1, "the scheduled prompt reaches omp", ct).ConfigureAwait(true);
                omp.Requests("session/prompt").ShouldHaveSingleItem()["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldBe("Summarise the inbox");
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task AProjectNotOnThisDeviceStartsNothingAndNamesTheCause()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp();
        var host = Host(root.Path, omp);
        await using var hostScope = host.ConfigureAwait(true);
        var starter = new ScheduledCoworkSessionStarter(host);

        var result = await starter.StartAsync(ScheduledSessionSurface.Cowork, "p-missing", [], SessionApprovalMode.Manual, null, "Summarise the inbox", ct).ConfigureAwait(true);

        result.Started.ShouldBeFalse();
        result.SessionId.ShouldBeNull();
        result.Error.ShouldNotBeNull().ShouldContain("not on this device");
        omp.Launches.Count.ShouldBe(0);
        host.TaskSummaries().ShouldBeEmpty();
    }

    [Fact]
    public async Task ARefusedOrMissingTrustedFolderStartsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp();
        var host = Host(root.Path, omp);
        await using var hostScope = host.ConfigureAwait(true);
        var starter = new ScheduledCoworkSessionStarter(host);
        var driveRoot = System.IO.Path.GetPathRoot(System.IO.Path.GetTempPath()) ?? "/";
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scheduled-cowork-absent-" + Guid.NewGuid().ToString("N"));

        var refused = await starter.StartAsync(ScheduledSessionSurface.Cowork, null, [driveRoot], SessionApprovalMode.Manual, null, "Summarise the inbox", ct).ConfigureAwait(true);
        var absent = await starter.StartAsync(ScheduledSessionSurface.Cowork, null, [missing], SessionApprovalMode.Manual, null, "Summarise the inbox", ct).ConfigureAwait(true);

        refused.Started.ShouldBeFalse();
        refused.Error.ShouldNotBeNull().ShouldContain("refused");
        absent.Started.ShouldBeFalse();
        absent.Error.ShouldNotBeNull().ShouldContain("not on this device");
        omp.Launches.Count.ShouldBe(0);
        host.TaskSummaries().ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipAndAModelChoiceAreRefusedBeforeAnyTaskExists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp();
        var host = Host(root.Path, omp);
        await using var hostScope = host.ConfigureAwait(true);
        var starter = new ScheduledCoworkSessionStarter(host);

        var skip = await starter.StartAsync(ScheduledSessionSurface.Cowork, null, [], SessionApprovalMode.Skip, null, "Summarise the inbox", ct).ConfigureAwait(true);
        var model = await starter.StartAsync(ScheduledSessionSurface.Cowork, null, [], SessionApprovalMode.Manual, "some-model", "Summarise the inbox", ct).ConfigureAwait(true);

        skip.Started.ShouldBeFalse();
        skip.Error.ShouldNotBeNull().ShouldContain("Skip");
        model.Started.ShouldBeFalse();
        model.Error.ShouldNotBeNull().ShouldContain("model");
        omp.Launches.Count.ShouldBe(0);
        host.TaskSummaries().ShouldBeEmpty();
    }

    [Fact]
    public async Task AHostThatCannotCreateReportsTheFailureAndStartsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp();
        var host = Host(root.Path, omp);
        await host.DisposeAsync().ConfigureAwait(true);
        var starter = new ScheduledCoworkSessionStarter(host);
        var result = await starter.StartAsync(ScheduledSessionSurface.Cowork, null, [], SessionApprovalMode.Manual, null, "Summarise the inbox", ct).ConfigureAwait(true);
        result.Started.ShouldBeFalse();
        result.SessionId.ShouldBeNull();
        result.Error.ShouldNotBeNull().ShouldContain("ObjectDisposedException");
        omp.Launches.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ARunnerStartThroughTheRealHostCreatesTheProjectTaskAndRecordsARefusalAsFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        using var outside = new TempDir();
        var projectFolder = Directory.CreateDirectory(System.IO.Path.Combine(root.Path, "project")).FullName;
        new Autonogrammer.Cowork.Projects.CoworkProjectStore(root.Path, "acct").Upsert(Project("p-1", projectFolder));
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var store = new InMemoryAutomationStore();
                var runner = new AutomationRunner(store, [], TimeProvider.System, [new ScheduledCoworkSessionStarter(host)]);
                await using var runnerScope = runner.ConfigureAwait(true);
                await store.SaveAsync(Automation("aut_inbox", "p-1", [outside.Path], "Review the inbox"), ct).ConfigureAwait(true);
                await store.SaveAsync(Automation("aut_gone", "p-missing", [], "Review the inbox"), ct).ConfigureAwait(true);

                var started = await runner.StartAsync(Automation("aut_inbox", "p-1", [outside.Path], "Review the inbox"), AutomationTriggerContext.Manual(), ct).ConfigureAwait(true);
                var entry = await started.Completion!.WaitAsync(ct).ConfigureAwait(true);
                entry.Status.ShouldBe(AutomationRunStatus.Launched);
                entry.Error.ShouldBeNull();
                var id = entry.ExternalId.ShouldNotBeNull();
                var meta = new CoworkSessionMetaStore(root.Path, "acct").Get(id).ShouldNotBeNull();
                meta.ProjectId.ShouldBe("p-1");
                meta.TrustedFolders.ShouldContain(outside.Path, StringComparer.Ordinal);
                omp.Launches.Count.ShouldBe(1);
                await AgentLoopFixtures.EventuallyAsync(() => omp.Requests("session/prompt").Count == 1, "the scheduled prompt reaches omp", ct).ConfigureAwait(true);
                omp.Requests("session/prompt").ShouldHaveSingleItem()["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldContain("Review the inbox");

                var refusedStart = await runner.StartAsync(Automation("aut_gone", "p-missing", [], "Review the inbox"), AutomationTriggerContext.Manual(), ct).ConfigureAwait(true);
                var refused = await refusedStart.Completion!.WaitAsync(ct).ConfigureAwait(true);
                refused.Status.ShouldBe(AutomationRunStatus.Failed);
                refused.ExternalId.ShouldBeNull();
                refused.Error.ShouldNotBeNull().ShouldContain("not on this device");
                omp.Launches.Count.ShouldBe(1);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    private static UserAutomation Automation(string id, string? projectId, string[] trustedFolders, string prompt) => new()
    {
        Id = id,
        Owner = "alice",
        Name = "Inbox review",
        Prompt = prompt,
        Enabled = true,
        Triggers = [new AutomationTriggerSpec { Kind = AutomationTriggerKind.Cron, Cron = "0 9 * * *" }],
        Target = new AutomationTargetSpec
        {
            Kind = AutomationTargetKind.Session,
            Surface = ScheduledSessionSurface.Cowork,
            ProjectId = projectId,
            TrustedFolders = trustedFolders,
            ApprovalMode = SessionApprovalMode.Manual,
        },
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    private static Autonogrammer.Cowork.Projects.CoworkProject Project(string id, string folder) => new()
    {
        Id = id,
        Name = "Project " + id,
        Folders = [folder],
    };

    private static CoworkSessionHost Host(string root, ScriptedOmp omp) =>
        new(new CoworkSessionHostOptions
        {
            StateRoot = root,
            WorkingDirectory = root,
            Gate = new Gate(),
            TransportFactory = omp.TransportFactory,
        });

    private sealed class Gate : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => "acct";
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scheduled-cowork-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
