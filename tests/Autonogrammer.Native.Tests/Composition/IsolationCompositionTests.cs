#pragma warning disable MA0006
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Abstractions;
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
        using var tree = new TempCoworkTree();
        var runner = new ScriptedRunner(IsolationProbeOutcome.Succeeded);
        var composition = new IsolationComposition(runner, "linux-x64");
        var cowork = await composition.PrepareAsync(true, tree.Home, tree.Scratch, [], false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var code = await composition.PrepareAsync(false, tree.Home, tree.Scratch, [], false, TestContext.Current.CancellationToken).ConfigureAwait(true);

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
        using var tree = new TempCoworkTree();
        var composition = new IsolationComposition(new ScriptedRunner(new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing)), "linux-x64");
        var blocked = await composition.PrepareAsync(true, tree.Home, tree.Scratch, [], false, TestContext.Current.CancellationToken).ConfigureAwait(true);
        blocked.SessionMayStart.ShouldBeFalse();
        blocked.ProcessWrapper.ShouldBeNull();
        blocked.Chip.NeedsConsent.ShouldBeTrue();

        var consented = await composition.PrepareAsync(true, tree.Home, tree.Scratch, [], true, TestContext.Current.CancellationToken).ConfigureAwait(true);
        consented.SessionMayStart.ShouldBeTrue();
        consented.ProcessWrapper.ShouldBeNull();
        consented.FilesystemIsolated.ShouldBeFalse();
        consented.Decision.Level.ShouldBe(IsolationLevel.None);
    }

    [Fact]
    public async Task AReadOnlyGrantGetsNoBindAndAReadWriteGrantGetsOne()
    {
        using var tree = new TempCoworkTree();
        var read = Path.Combine(tree.Root, "read");
        var write = Path.Combine(tree.Root, "write");
        Directory.CreateDirectory(read);
        Directory.CreateDirectory(write);
        var grants = new CoworkFolderGrant[]
        {
            new(read, CoworkFolderAccess.ReadOnly, CoworkFolderScope.Global),
            new(write, CoworkFolderAccess.ReadWrite, CoworkFolderScope.Global),
        };
        var composition = new IsolationComposition(new ScriptedRunner(IsolationProbeOutcome.Succeeded), "linux-x64");
        var start = await composition.PrepareAsync(true, tree.Home, tree.Scratch, grants, false, TestContext.Current.CancellationToken).ConfigureAwait(true);

        start.Grants.Single(grant => grant.Path == read).ReadWrite.ShouldBeFalse();
        start.Grants.Single(grant => grant.Path == write).ReadWrite.ShouldBeTrue();
    }

    private sealed class TempCoworkTree : IDisposable
    {
        public TempCoworkTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "autonogrammer-native-isolation-" + Guid.NewGuid().ToString("N"));
            Home = Path.Combine(Root, "home");
            Scratch = Path.Combine(Root, "scratch");
            Directory.CreateDirectory(Path.Combine(Home, ".omp"));
            Directory.CreateDirectory(Scratch);
        }

        public string Root { get; }

        public string Home { get; }

        public string Scratch { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
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

#pragma warning restore MA0006
