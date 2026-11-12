
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Automations.Scheduling;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The scheduled Cowork starter over the real host. No live omp is started.</summary>
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

