using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Isolation;
using Autonogrammer.Native.Composition.Isolation;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>Cowork carries the wrapper. Code does not. A failed probe starts nothing until consent.</summary>
public sealed class IsolationCompositionTests
{
    [Fact]
    public async Task CoworkOptionsCarryTheWrapperAndCodeOptionsCarryNull()
    {
        var runner = new ScriptedRunner(IsolationProbeOutcome.Succeeded);
        var composition = new IsolationComposition(runner, "linux-x64");
        var cowork = await composition.PrepareAsync(true, "/home/user", "/tmp/scratch", [], false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var code = await composition.PrepareAsync(false, "/home/user", "/tmp/scratch", [], false, TestContext.Current.CancellationToken).ConfigureAwait(true);

        cowork.ProcessWrapper.ShouldNotBeNull();
        cowork.FilesystemIsolated.ShouldBeTrue();
        code.ProcessWrapper.ShouldBeNull();
        var coworkLaunch = await LaunchAsync(cowork.ProcessWrapper).ConfigureAwait(true);
        var codeLaunch = await LaunchAsync(code.ProcessWrapper).ConfigureAwait(true);
        coworkLaunch.ProcessWrapper.ShouldBeSameAs(cowork.ProcessWrapper);
        codeLaunch.ProcessWrapper.ShouldBeNull();
    }

    [Fact]
    public async Task AFailedProbeAsksAndStartsNothingUntilConsent()
    {
        var composition = new IsolationComposition(new ScriptedRunner(new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing)), "linux-x64");
        var blocked = await composition.PrepareAsync(true, "/home/user", "/tmp/scratch", [], false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        blocked.SessionMayStart.ShouldBeFalse();
        blocked.ProcessWrapper.ShouldBeNull();
        blocked.Chip.NeedsConsent.ShouldBeTrue();

        var consented = await composition.PrepareAsync(true, "/home/user", "/tmp/scratch", [], true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        consented.SessionMayStart.ShouldBeTrue();
        consented.ProcessWrapper.ShouldBeNull();
        consented.FilesystemIsolated.ShouldBeFalse();
        consented.Decision.Level.ShouldBe(IsolationLevel.None);
    }

    [Fact]
    public async Task AReadOnlyGrantGetsNoBindAndAReadWriteGrantGetsOne()
    {
        var grants = new CoworkFolderGrant[]
        {
            new("/tmp/read", CoworkFolderAccess.ReadOnly, CoworkFolderScope.Global),
            new("/tmp/write", CoworkFolderAccess.ReadWrite, CoworkFolderScope.Global),
        };
        var composition = new IsolationComposition(new ScriptedRunner(IsolationProbeOutcome.Succeeded), "linux-x64");
        var start = await composition.PrepareAsync(true, "/home/user", "/tmp/scratch", grants, false, TestContext.Current.CancellationToken).ConfigureAwait(true);

        start.Grants.Single(grant => grant.Path == "/tmp/read").ReadWrite.ShouldBeFalse();
        start.Grants.Single(grant => grant.Path == "/tmp/write").ReadWrite.ShouldBeTrue();
    }

    private static async Task<AcpClientOptions> LaunchAsync(IProcessLaunchWrapper? wrapper)
    {
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        var plan = new OmpLaunchPlan
        {
            ExtraArguments = [],
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            McpServers = [],
            WorkingDirectory = "/work",
            PlanningEvents = [],
        };
        var engine = EngineComposition.Compose(["read"], plan, omp.TransportFactory, options: new EngineCompositionOptions { ProcessWrapper = wrapper });
        await using var host = engine.Host.ConfigureAwait(true);
        await engine.Host.ConnectAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return omp.Launches.ShouldHaveSingleItem();
    }

    private sealed class ScriptedRunner(IsolationProbeOutcome outcome) : IIsolationProcessRunner
    {
        public Task<IsolationProcessRun> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var missing = outcome.DegradedCode == IsolationProbeOutcome.BwrapMissing;
            var failed = outcome.DegradedCode is not null;
            return Task.FromResult(new IsolationProcessRun(failed ? 1 : 0, string.Empty, false, missing));
        }
    }
}
