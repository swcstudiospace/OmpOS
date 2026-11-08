using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>Check, the confirmation gate, idempotence, unreachable machines and failing installs, through a transport that counts writes.</summary>
public sealed class RemoteProvisionServiceTests
{
    private static readonly DateTimeOffset Observed = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly ProvisionComponent[] ExpectedDriftOrder = [ProvisionComponent.Bun, ProvisionComponent.Omp, ProvisionComponent.Plugin];
    private static readonly ProvisionActionStatus[] ExpectedOutcomeOrder = [ProvisionActionStatus.Succeeded, ProvisionActionStatus.Failed, ProvisionActionStatus.NotRun];
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The check of a machine with an old Bun, no omp, and one plugin missing reports each item with its reason and never writes.</summary>
    [Fact]
    public async Task CheckReportsEachDriftItemWithItsReasonAndNeverWrites()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var check = result.ShouldBeOfType<ProvisionCheck>();
        check.Drift.Select(static item => item.Component).ShouldBe(ExpectedDriftOrder);
        check.Drift[0].Reason.ShouldContain("1.3.0");
        check.Drift[1].Reason.ShouldContain("not installed");
        check.Drift[2].Subject.ShouldEndWith("/claude-ultrathink");
        check.Drift.ShouldAllBe(item => item.IsActionable);
        check.Actions.Count.ShouldBe(3);
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A plugin whose recorded commit differs from the bundle's is drift, with both commits in the reason.</summary>
    [Fact]
    public async Task ChangedPluginCommitIsPluginDrift()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.Probe(
            ompVersion: Fixtures.LatestVersion,
            bunVersion: OmpBunPin.Default.Version,
            bundleJson: Fixtures.RemoteBundle(("gsd-core-omp", Fixtures.CommitA), ("claude-ultrathink", Fixtures.CommitC)))));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var item = check.Drift.Single();
        item.Component.ShouldBe(ProvisionComponent.Plugin);
        item.Reason.ShouldContain(Fixtures.CommitC[..12]);
        item.Reason.ShouldContain(Fixtures.CommitB[..12]);
        check.Actions.Count.ShouldBe(1);
    }

    /// <summary>An in-repo plugin the remote machine lacks is reported as blocked, is never an action, and is never written.</summary>
    [Fact]
    public async Task InRepoPluginIsReportedBlockedAndNeverInstalled()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.Probe(
            ompVersion: Fixtures.LatestVersion,
            bunVersion: OmpBunPin.Default.Version,
            bundleJson: Fixtures.RemoteBundle(("claude-ultrathink", Fixtures.CommitB)))));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var blocked = check.Drift.Single();
        blocked.Subject.ShouldEndWith("/gsd-core-omp");
        blocked.IsActionable.ShouldBeFalse();
        blocked.BlockedBecause.ShouldNotBeNull().ShouldContain("cannot fetch it");
        check.Actions.ShouldBeEmpty();
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A machine that matches the desired state reports no drift, and a provision of it makes no write.</summary>
    [Fact]
    public async Task UpToDateMachineIsNotWritten()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.AfterInstall()));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var provisioned = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var check = provisioned.ShouldBeOfType<ProvisionCheck>();
        check.Drift.ShouldBeEmpty();
        check.Actions.ShouldBeEmpty();
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>Without a recorded confirmation the provision returns the action list and writes nothing.</summary>
    [Fact]
    public async Task ProvisionWithoutConfirmationReturnsNeedsConfirmationAndWritesNothing()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var needs = result.ShouldBeOfType<ProvisionNeedsConfirmation>();
        needs.Actions.Count.ShouldBe(3);
        needs.Actions[0].ShouldBe("Install Bun 1.4.4 into " + Fixtures.Home + "/.bun/bin/bun");
        needs.Actions[1].ShouldStartWith("Install Omp 18.18.1 into " + Fixtures.Home + "/.local/bin/omp");
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A confirmation for a different action list is not accepted: the provision asks again and writes nothing.</summary>
    [Fact]
    public async Task ConfirmationForADifferentActionListIsNotAccepted()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var store = new InMemoryProvisionConfirmationStore();
        await store.RecordAsync(new ProvisionConfirmation("env-1", ["Install Bun 1.4.4 into " + Fixtures.Home + "/.bun/bin/bun"], Observed), Token).ConfigureAwait(true);
        var service = Service(new FakeReleaseSource(Fixtures.Latest()), store);

        var result = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        result.ShouldBeOfType<ProvisionNeedsConfirmation>();
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>With the exact confirmation, only the drifted items are written, in the order Bun, Omp, plugin, each with its own script.</summary>
    [Fact]
    public async Task ConfirmedProvisionWritesOnlyTheDriftedItemsInOrder()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var store = new InMemoryProvisionConfirmationStore();
        var service = Service(new FakeReleaseSource(Fixtures.Latest()), store);
        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);
        await store.RecordAsync(new ProvisionConfirmation("env-1", check.Actions, Observed), Token).ConfigureAwait(true);

        var result = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var completed = result.ShouldBeOfType<ProvisionCompleted>();
        completed.Succeeded.ShouldBeTrue();
        completed.Outcomes.Select(outcome => outcome.Status).ShouldAllBe(status => status == ProvisionActionStatus.Succeeded);
        exec.WriteCount.ShouldBe(3);
        exec.WrittenScripts[0].ShouldContain("bun-v1.4.4/bun-linux-x64.zip");
        exec.WrittenScripts[1].ShouldContain("pi-coding-agent-18.18.1.tgz");
        exec.WrittenScripts[2].ShouldContain("'" + Fixtures.UltrathinkSource + "'");
        exec.WrittenScripts[2].ShouldContain("\"dir\":\"gsd-core-omp\"");
    }

    /// <summary>After a successful install, the next provision reads the machine as up to date and makes no further write.</summary>
    [Fact]
    public async Task SecondProvisionAfterASuccessfulInstallMakesNoWrites()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.AfterInstall()));
        var store = new InMemoryProvisionConfirmationStore();
        var service = Service(new FakeReleaseSource(Fixtures.Latest()), store);
        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);
        await store.RecordAsync(new ProvisionConfirmation("env-1", check.Actions, Observed), Token).ConfigureAwait(true);

        var first = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);
        var writesAfterFirst = exec.WriteCount;
        var second = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        first.ShouldBeOfType<ProvisionCompleted>();
        writesAfterFirst.ShouldBe(3);
        var up = second.ShouldBeOfType<ProvisionCheck>();
        up.Drift.ShouldBeEmpty();
        exec.WriteCount.ShouldBe(writesAfterFirst);
    }

    /// <summary>An unreachable machine returns the last snapshot with its own timestamp, and nothing is written.</summary>
    [Fact]
    public async Task UnreachableMachineReturnsTheLastSnapshotAndItsTimestamp()
    {
        var earlier = new DateTimeOffset(2026, 11, 7, 18, 30, 0, TimeSpan.Zero);
        var lastKnown = RemoteProbeParser.Parse(Fixtures.BeforeInstall(), earlier).State.ShouldNotBeNull();
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(new RemoteExecResult(255, ReadOnlyMemory<byte>.Empty, "ssh: connect to host remote-a port 22: Connection refused", false, false, null));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.CheckAsync(Fixtures.Request(lastKnown), exec, Token).ConfigureAwait(true);

        var unavailable = result.ShouldBeOfType<ProvisionUnavailable>();
        unavailable.LastKnown.ShouldBeSameAs(lastKnown);
        unavailable.LastKnown.ShouldNotBeNull().ObservedAt.ShouldBe(earlier);
        unavailable.Reason.ShouldNotBeNullOrWhiteSpace();
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A timed-out machine is unavailable too, and carries no snapshot when none was passed in.</summary>
    [Fact]
    public async Task TimedOutMachineIsUnavailableWithoutAKnownSnapshot()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(new RemoteExecResult(-1, ReadOnlyMemory<byte>.Empty, string.Empty, false, TimedOut: true, StartFailure: null));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var unavailable = result.ShouldBeOfType<ProvisionUnavailable>();
        unavailable.LastKnown.ShouldBeNull();
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A probe answer that does not parse is reported as a probe failure with an excerpt, never as a state.</summary>
    [Fact]
    public async Task UnreadableProbeAnswerIsAProbeFailureNotAGuess()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok("zsh: no such file\n"));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var failed = result.ShouldBeOfType<ProvisionProbeFailed>();
        failed.Reason.ShouldNotBeNullOrWhiteSpace();
        failed.Excerpt.ShouldBe("zsh: no such file");
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A probe that exits with an error is a probe failure that names the exit code.</summary>
    [Fact]
    public async Task ProbeExitingWithAnErrorIsAProbeFailure()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Failed(2, "sh: syntax error"));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        result.ShouldBeOfType<ProvisionProbeFailed>().Reason.ShouldContain("2");
    }

    /// <summary>A platform no pinned Bun runs on is refused before any drift is planned, and nothing is written.</summary>
    [Fact]
    public async Task UnsupportedPlatformIsRefusedWithoutWrites()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.Probe(kernel: "FreeBSD", machine: "amd64")));
        var service = Service(new FakeReleaseSource(Fixtures.Latest()));

        var result = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        var unsupported = result.ShouldBeOfType<ProvisionUnsupportedPlatform>();
        unsupported.KernelName.ShouldBe("FreeBSD");
        unsupported.Machine.ShouldBe("amd64");
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>An unreachable registry means the desired omp is unknown, so nothing is installed.</summary>
    [Fact]
    public async Task UnreachableRegistryMakesNoWrite()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var service = Service(new FakeReleaseSource(release: null, failure: new HttpRequestException("registry down")));

        var result = await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        result.ShouldBeOfType<ProvisionDesiredStateUnavailable>().Reason.ShouldContain("registry down");
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A release that needs a newer Bun than the pin is refused, and so is one whose range this build cannot check.</summary>
    [Theory]
    [InlineData(">=1.5.0", "1.5.0")]
    [InlineData("^1.4.4", "^1.4.4")]
    public async Task ReleaseThePinnedBunDoesNotMeetIsRefused(string engines, string mentioned)
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var service = Service(new FakeReleaseSource(Fixtures.Latest(engines)));

        var result = await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        result.ShouldBeOfType<ProvisionDesiredStateUnavailable>().Reason.ShouldContain(mentioned);
        exec.WriteCount.ShouldBe(0);
    }

    /// <summary>A profile with its own omp path leaves omp alone: no omp drift, and the registry is not read.</summary>
    [Fact]
    public async Task CustomOmpPathLeavesOmpAloneAndDoesNotReadTheRegistry()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        var releases = new FakeReleaseSource(release: null, failure: new HttpRequestException("must not be read"));
        var service = Service(releases);

        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(profile: Fixtures.Profile("/opt/omp/bin/omp")), exec, Token).ConfigureAwait(true);

        check.Drift.Select(static item => item.Component).ShouldNotContain(ProvisionComponent.Omp);
        releases.Reads.ShouldBe(0);
    }

    /// <summary>The first failed install stops the rest: later actions are reported not run and never sent.</summary>
    [Fact]
    public async Task FailedInstallStopsLaterActionsAndReportsThem()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueWriteAnswer(FakeRemoteExec.Ok(string.Empty));
        exec.EnqueueWriteAnswer(FakeRemoteExec.Failed(1, "autonogrammer-provision: the Omp tarball does not match the registry SHA-512\n"));
        var store = new InMemoryProvisionConfirmationStore();
        var service = Service(new FakeReleaseSource(Fixtures.Latest()), store);
        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);
        await store.RecordAsync(new ProvisionConfirmation("env-1", check.Actions, Observed), Token).ConfigureAwait(true);

        var completed = (ProvisionCompleted)await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        completed.Succeeded.ShouldBeFalse();
        completed.Outcomes.Select(static outcome => outcome.Status).ShouldBe(ExpectedOutcomeOrder);
        completed.Outcomes[1].Message.ShouldNotBeNull().ShouldContain("Omp tarball");
        completed.Outcomes[2].Message.ShouldBe(RemoteProvisionMessages.NotRunReason);
        exec.WriteCount.ShouldBe(2);
    }

    /// <summary>A failure message carries at most 200 characters of the script's output.</summary>
    [Fact]
    public async Task FailureMessageIsCappedAtTwoHundredCharacters()
    {
        using var exec = new FakeRemoteExec();
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueRead(FakeRemoteExec.Ok(Fixtures.BeforeInstall()));
        exec.EnqueueWriteAnswer(FakeRemoteExec.Failed(1, new string('x', 1000)));
        var store = new InMemoryProvisionConfirmationStore();
        var service = Service(new FakeReleaseSource(Fixtures.Latest()), store);
        var check = (ProvisionCheck)await service.CheckAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);
        await store.RecordAsync(new ProvisionConfirmation("env-1", check.Actions, Observed), Token).ConfigureAwait(true);

        var completed = (ProvisionCompleted)await service.ProvisionAsync(Fixtures.Request(), exec, Token).ConfigureAwait(true);

        completed.Outcomes[0].Message.ShouldNotBeNull().Length.ShouldBeLessThanOrEqualTo(RemoteOutputExcerpt.MaxCharacters);
    }

    private static RemoteProvisionService Service(IOmpReleaseSource releases, IProvisionConfirmationStore? store = null) =>
        new(releases, store ?? new InMemoryProvisionConfirmationStore(), new FixedClock(Observed));
}
