using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Composition;
using Autonogrammer.Presentation.Landing;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>The managed omp install as the Code landing runs it: the release is read, then that release is installed, each stage is reported, and every launcher outcome becomes a notice. The managed install is a fake; nothing reaches the network.</summary>
public sealed class LocalOmpInstallerTests
{
    private static readonly DateTimeOffset CheckedAt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Gets each launcher outcome and the notice it must become.</summary>
    public static TheoryData<OmpUpdateResult, LocalOmpUpdate> LauncherOutcomes => new()
    {
        { new OmpUpdated("2.0.0", "1.0.0", "Omp 2.0.0 is active."), new LocalOmpUpdate(LocalOmpUpdateState.Updated, "2.0.0", null, "Omp 2.0.0 is active.") },
        { new OmpUpToDate("2.0.0", CheckedAt, "Up to date."), new LocalOmpUpdate(LocalOmpUpdateState.UpToDate, "2.0.0", CheckedAt, "Up to date.") },
        { new OmpOffline("2.0.0", CheckedAt, "Offline."), new LocalOmpUpdate(LocalOmpUpdateState.Offline, "2.0.0", CheckedAt, "Offline.") },
        { new OmpDeferred("2.1.0", "In use."), new LocalOmpUpdate(LocalOmpUpdateState.Deferred, "2.1.0", null, "In use.") },
        { new OmpRolledBack("2.1.0", "2.0.0", "Rolled back."), new LocalOmpUpdate(LocalOmpUpdateState.RolledBack, "2.0.0", null, "Rolled back.") },
        { new OmpBroken("2.0.0", true, "Does not match its digest."), new LocalOmpUpdate(LocalOmpUpdateState.Failed, "2.0.0", null, "Does not match its digest.") },
        { new OmpUpdateFailed(OmpFailureKind.Permission, "2.0.0", "No access."), new LocalOmpUpdate(LocalOmpUpdateState.Failed, null, null, "No access.") },
    };

    /// <summary>The install reads the latest release, installs that same release, reports both stages, and returns the new version.</summary>
    [Fact]
    public async Task InstallReadsTheLatestReleaseThenInstallsThatRelease()
    {
        var release = FakeOmpManagedInstall.ReleaseOf("2.0.0");
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => Task.FromResult(release),
            Install = (_, _) => Task.FromResult<OmpUpdateResult>(new OmpUpdated("2.0.0", "1.0.0", "Omp 2.0.0 is active.")),
        };
        var installer = new LocalOmpInstaller(managed);
        var stages = new List<LocalOmpStatus>();

        var result = await installer.InstallAsync(stages.Add, Token);

        managed.ReadRequests.ShouldBe(new string?[] { null });
        managed.Installs.ShouldHaveSingleItem().ShouldBeSameAs(release);
        stages.ShouldBe(
        [
            LocalOmpStatus.Installing(LocalOmpInstallStage.ReadingRelease, null),
            LocalOmpStatus.Installing(LocalOmpInstallStage.Installing, "2.0.0"),
        ]);
        result.ShouldBe(new LocalOmpUpdate(LocalOmpUpdateState.Updated, "2.0.0", null, "Omp 2.0.0 is active."));
    }

    /// <summary>An install the launcher refuses is returned with its state and reason, never as a success.</summary>
    [Fact]
    public async Task AnInstallTheLauncherRefusesIsReturnedWithItsReason()
    {
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
            Install = (_, _) => Task.FromResult<OmpUpdateResult>(new OmpUpdateFailed(OmpFailureKind.IntegrityFailed, "2.0.0", "The download does not match its digest.")),
        };

        var result = await new LocalOmpInstaller(managed).InstallAsync(_ => { }, Token);

        result.State.ShouldBe(LocalOmpUpdateState.Failed);
        result.Reason.ShouldBe("The download does not match its digest.");
    }

    /// <summary>A registry that cannot be reached ends the install before anything is installed, and says so.</summary>
    [Fact]
    public async Task AnUnreachableRegistryEndsTheInstallBeforeAnythingIsInstalled()
    {
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => throw new HttpRequestException("The registry is down."),
        };

        var result = await new LocalOmpInstaller(managed).InstallAsync(_ => { }, Token);

        result.State.ShouldBe(LocalOmpUpdateState.Offline);
        result.Reason.ShouldBe(OmpManagedMessages.Offline(version: null, checkedAt: null));
        managed.Installs.ShouldBeEmpty();
    }

    /// <summary>A registry answer that is not a usable package record is a failure with the launcher's message.</summary>
    [Fact]
    public async Task AnInvalidRegistryAnswerIsAFailureWithItsMessage()
    {
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => throw new InvalidDataException("No versions."),
        };

        var result = await new LocalOmpInstaller(managed).InstallAsync(_ => { }, Token);

        result.State.ShouldBe(LocalOmpUpdateState.Failed);
        result.Reason.ShouldBe(OmpManagedMessages.InvalidRegistryResponse("No versions."));
        managed.Installs.ShouldBeEmpty();
    }

    /// <summary>Cancelling an install throws the cancellation, so the landing returns to the status it had.</summary>
    [Fact]
    public async Task CancellingTheInstallThrowsTheCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
            Install = async (_, cancellationToken) =>
            {
                await cancellation.CancelAsync().ConfigureAwait(false);
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return new OmpUpdated("2.0.0", null, "unreachable");
            },
        };

        await Should.ThrowAsync<OperationCanceledException>(() => new LocalOmpInstaller(managed).InstallAsync(_ => { }, cancellation.Token));
    }

    /// <summary>Each launcher outcome of a check becomes the notice the landing shows for it.</summary>
    /// <param name="outcome">The launcher's outcome.</param>
    /// <param name="expected">The notice.</param>
    [Theory]
    [MemberData(nameof(LauncherOutcomes))]
    public void EachLauncherOutcomeBecomesItsNotice(OmpUpdateResult outcome, LocalOmpUpdate expected) =>
        LocalOmpInstaller.Map(outcome).ShouldBe(expected);

    /// <summary>A check that throws an unexpected error is a failed notice rather than a crash of the landing.</summary>
    [Fact]
    public async Task AnUnexpectedFailureOfTheCheckIsAFailedNotice()
    {
        var managed = new FakeOmpManagedInstall
        {
            Check = _ => throw new IOException("The state record is locked."),
        };

        var notice = await new LocalOmpInstaller(managed).CheckAsync(Token);

        notice.ShouldBe(new LocalOmpUpdate(LocalOmpUpdateState.Failed, null, null, "The state record is locked."));
    }
}
