using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// The Omp part of the Remote environments page: a check shows the lines an install would apply, Install Omp applies exactly those lines once, and every
/// other outcome (up to date, stale, refused, unavailable, failed, cancelled) says what happened and offers no install until the machine is checked again.
/// </summary>
public sealed class RemoteOmpPageTests
{
    private const string BunLine = "Install Bun 1.4.4 into /home/tester/.bun/bin/bun";
    private const string OmpLine = "Install Omp 18.18.1 into /home/tester/.local/bin/omp (built with Bun 1.4.4)";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Loads one saved ssh environment, with the given Omp provisioner, or none.</summary>
    private static async Task<(RemoteEnvironmentsViewModel Page, RemoteProfileRow Row)> LoadOneAsync(FakeRemoteOmpProvisioner? omp)
    {
        var host = new FakeRemoteEnvironmentsHost().With(new EnvironmentProfile
        {
            Id = "a",
            DisplayName = "Box a",
            Kind = EnvironmentKind.Ssh,
            Ssh = new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/srv/app" },
        });
        var page = new RemoteEnvironmentsViewModel(host, omp);
        await page.LoadAsync(Token).ConfigureAwait(true);
        return (page, page.Rows.ShouldHaveSingleItem());
    }

    /// <summary>A check that planned the Bun and Omp lines, with the Bun drift that causes the first one.</summary>
    private static RemoteOmpReport PlannedCheck() => new(
        RemoteOmpOutcome.Planned,
        [new RemoteOmpDriftLine("/home/tester/.bun/bin/bun", "Bun 1.3.0 is installed; the pinned version is 1.4.4.", null)],
        [BunLine, OmpLine],
        null,
        false);

    private static RemoteOmpReport Report(RemoteOmpOutcome outcome, string? reason = null, bool changed = false) => new(outcome, [], [], reason, changed);

    /// <summary>A check lists the lines an install would apply and offers Install Omp for exactly those lines; it writes nothing.</summary>
    [Fact]
    public async Task ACheckShowsItsLinesAndOffersInstallForThem()
    {
        var omp = new FakeRemoteOmpProvisioner().ReplyToCheck(PlannedCheck());
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);

        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        row.OmpOffered.ShouldBeTrue();
        omp.CheckRequests.ShouldBe(["a"]);
        row.OmpActionLines.ShouldBe([BunLine, OmpLine]);
        row.HasOmpActions.ShouldBeTrue();
        row.OmpDriftLines.ShouldHaveSingleItem().ShouldContain("Bun 1.3.0");
        row.OmpResultText.ShouldBe(RemoteCopy.OmpPlanned);
        row.CanInstallOmp.ShouldBeTrue();
        page.Announcement.ShouldBe("Box a: " + RemoteCopy.OmpPlanned);
        omp.InstallRequests.ShouldBeEmpty();
    }

    /// <summary>Install Omp applies exactly the lines the check showed, once; afterwards the page needs a new check before another install.</summary>
    [Fact]
    public async Task InstallAppliesExactlyTheCheckedLinesOnce()
    {
        var omp = new FakeRemoteOmpProvisioner()
            .ReplyToCheck(PlannedCheck())
            .ReplyToInstall(Report(RemoteOmpOutcome.Installed, changed: true));
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);
        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        await page.InstallOmpAsync(row, Token).ConfigureAwait(true);

        var request = omp.InstallRequests.ShouldHaveSingleItem();
        request.EnvironmentId.ShouldBe("a");
        request.Lines.ShouldBe([BunLine, OmpLine]);
        row.OmpResultText.ShouldBe(RemoteCopy.OmpInstalled);
        row.CanInstallOmp.ShouldBeFalse();
        row.CanCheckOmp.ShouldBeTrue();

        await page.InstallOmpAsync(row, Token).ConfigureAwait(true);

        omp.InstallRequests.ShouldHaveSingleItem();
    }

    /// <summary>Install Omp is never sent when no check planned lines.</summary>
    [Fact]
    public async Task InstallWithoutACheckIsNotSent()
    {
        var omp = new FakeRemoteOmpProvisioner();
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);

        row.CanInstallOmp.ShouldBeFalse();
        await page.InstallOmpAsync(row, Token).ConfigureAwait(true);

        omp.InstallRequests.ShouldBeEmpty();
    }

    /// <summary>A machine that is already current reports so, lists no lines and offers no install.</summary>
    [Fact]
    public async Task AnUpToDateMachineOffersNoInstall()
    {
        var omp = new FakeRemoteOmpProvisioner().ReplyToCheck(Report(RemoteOmpOutcome.UpToDate));
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);

        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        row.OmpResultText.ShouldBe(RemoteCopy.OmpUpToDate);
        row.OmpActionLines.ShouldBeEmpty();
        row.HasOmpActions.ShouldBeFalse();
        row.CanInstallOmp.ShouldBeFalse();
    }

    /// <summary>An install that found the machine changed since its check says so, and offers no install until the user checks again.</summary>
    [Fact]
    public async Task AStaleInstallAsksForANewCheckAndOffersNoInstall()
    {
        var omp = new FakeRemoteOmpProvisioner()
            .ReplyToCheck(PlannedCheck())
            .ReplyToInstall(Report(RemoteOmpOutcome.NeedsCheck));
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);
        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        await page.InstallOmpAsync(row, Token).ConfigureAwait(true);

        row.OmpResultText.ShouldBe(RemoteCopy.OmpNeedsCheck);
        row.CanInstallOmp.ShouldBeFalse();
        page.Announcement.ShouldBe("Box a: " + RemoteCopy.OmpNeedsCheck);
    }

    /// <summary>A refusal and an unreachable machine each say their reason, and neither offers an install.</summary>
    [Fact]
    public async Task RefusalAndUnavailabilityShowTheirReason()
    {
        var omp = new FakeRemoteOmpProvisioner()
            .ReplyToCheck(Report(RemoteOmpOutcome.Refused, "Needs curl; not on the remote path."))
            .ReplyToCheck(Report(RemoteOmpOutcome.Unavailable, "The remote machine did not answer in time."));
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);

        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);
        row.OmpResultText.ShouldBe(RemoteCopy.OmpRefusedPrefix + "Needs curl; not on the remote path.");
        row.CanInstallOmp.ShouldBeFalse();

        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);
        row.OmpResultText.ShouldBe(RemoteCopy.OmpUnavailablePrefix + "The remote machine did not answer in time.");
        page.Announcement.ShouldContain("did not answer in time");
        row.CanInstallOmp.ShouldBeFalse();
    }

    /// <summary>A failed install names the reason and says that lines may already be applied, so the user checks before another install.</summary>
    [Fact]
    public async Task AFailedInstallSaysThatLinesMayAlreadyBeApplied()
    {
        var omp = new FakeRemoteOmpProvisioner()
            .ReplyToCheck(PlannedCheck())
            .ReplyToInstall(Report(RemoteOmpOutcome.Failed, "The install exited with code 1.", changed: true));
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);
        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        await page.InstallOmpAsync(row, Token).ConfigureAwait(true);

        row.OmpResultText.ShouldBe(RemoteCopy.OmpFailedPrefix + "The install exited with code 1. " + RemoteCopy.OmpFailedChanged + RemoteCopy.OmpFailedEnding);
        row.CanInstallOmp.ShouldBeFalse();
    }

    /// <summary>A check that throws becomes an unavailable result, and the row is free to check again.</summary>
    [Fact]
    public async Task ACheckThatThrowsBecomesAnUnavailableResult()
    {
        var omp = new FakeRemoteOmpProvisioner { Failure = new InvalidOperationException("the registry is down") };
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);

        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        row.OmpResultText.ShouldBe(RemoteCopy.OmpUnavailablePrefix + "The check could not run. the registry is down");
        row.IsOmpBusy.ShouldBeFalse();
        row.CanCheckOmp.ShouldBeTrue();
    }

    /// <summary>A cancelled check leaves the row as it was, with nothing shown.</summary>
    [Fact]
    public async Task ACancelledCheckLeavesTheRowIdle()
    {
        var omp = new FakeRemoteOmpProvisioner { Failure = new OperationCanceledException() };
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);

        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        row.IsOmpBusy.ShouldBeFalse();
        row.HasOmpResult.ShouldBeFalse();
        page.Announcement.ShouldBe("The Omp check of Box a was cancelled.");
    }

    /// <summary>A cancelled install may have written lines, so it offers no install of the same lines.</summary>
    [Fact]
    public async Task ACancelledInstallOffersNoInstallOfTheSameLines()
    {
        var omp = new FakeRemoteOmpProvisioner().ReplyToCheck(PlannedCheck());
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);
        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);
        omp.Failure = new OperationCanceledException();

        await page.InstallOmpAsync(row, Token).ConfigureAwait(true);

        row.OmpResultText.ShouldStartWith(RemoteCopy.OmpFailedPrefix);
        row.IsOmpBusy.ShouldBeFalse();
        row.CanInstallOmp.ShouldBeFalse();
    }

    /// <summary>Without an Omp provisioner the head offers no Omp buttons, and a call does nothing.</summary>
    [Fact]
    public async Task WithoutAProvisionerTheOmpButtonsAreNotOffered()
    {
        var (page, row) = await LoadOneAsync(omp: null).ConfigureAwait(true);

        row.OmpOffered.ShouldBeFalse();
        row.CanCheckOmp.ShouldBeFalse();
        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);

        row.HasOmpResult.ShouldBeFalse();
    }

    /// <summary>A reload while an install runs keeps no planned lines on the new row, so Install Omp is not offered while the write may still be running.</summary>
    [Fact]
    public async Task AReloadWhileAnInstallRunsOffersNoInstall()
    {
        var gate = new TaskCompletionSource<RemoteOmpReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        var omp = new FakeRemoteOmpProvisioner { InstallGate = gate }.ReplyToCheck(PlannedCheck());
        var (page, row) = await LoadOneAsync(omp).ConfigureAwait(true);
        await page.CheckOmpAsync(row, Token).ConfigureAwait(true);
        var running = page.InstallOmpAsync(row, Token);

        await page.LoadAsync(Token).ConfigureAwait(true);
        var reloaded = page.Rows.ShouldHaveSingleItem();

        reloaded.CanInstallOmp.ShouldBeFalse();
        reloaded.HasOmpResult.ShouldBeFalse();

        gate.SetResult(Report(RemoteOmpOutcome.Installed, changed: true));
        await running.ConfigureAwait(true);
        row.OmpResultText.ShouldBe(RemoteCopy.OmpInstalled);
        reloaded.CanInstallOmp.ShouldBeFalse();
    }
}
