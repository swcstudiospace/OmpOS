using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Landing;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>
/// The managed omp actions of the Local machine card in the shell: Install Omp runs the managed install with its progress, a failed install offers
/// Try again, a cancel returns the card to its status, and the update check shows its notice after the window is shown. The managed install and the
/// status are fakes; nothing reaches the network or starts an omp.
/// </summary>
public sealed class CodeLandingInstallShellTests
{
    private const string Guidance = "Install omp.";
    private static readonly DateTimeOffset CheckedAt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private const string InstallName = CodeLandingCopy.InstallOmp + ", " + CodeLandingCopy.LocalTitle;
    private const string RetryName = CodeLandingCopy.Retry + ", " + CodeLandingCopy.LocalTitle;
    private const string CancelName = CodeLandingCopy.CancelInstall + ", " + CodeLandingCopy.LocalTitle;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Activating Install reads the release, installs that release, and then shows the installed version.</summary>
    [Fact]
    public async Task ActivatingInstallReadsTheReleaseInstallsItAndShowsTheInstalledVersion()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var installed = false;
            var managed = new FakeOmpManagedInstall
            {
                ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
                Install = (_, _) =>
                {
                    installed = true;
                    return Task.FromResult<OmpUpdateResult>(new OmpUpdated("2.0.0", null, "Omp 2.0.0 is active."));
                },
            };
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(Services(_ => Task.FromResult(Status(installed)), managed));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp is not installed. " + Guidance, StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, InstallName));

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp 2.0.0 from the managed install. Omp 2.0.0 is now the managed install.", StringComparer.Ordinal)).ConfigureAwait(true);
            managed.ReadRequests.ShouldBe(new string?[] { null });
            managed.Installs.ShouldHaveSingleItem().Version.ShouldBe("2.0.0");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A failed install shows its reason, and Try again installs again until it succeeds.</summary>
    [Fact]
    public async Task AFailedInstallShowsItsReasonAndTryAgainInstallsAgain()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var installed = false;
            var attempts = 0;
            var managed = new FakeOmpManagedInstall
            {
                ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
                Install = (_, _) =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        return Task.FromResult<OmpUpdateResult>(new OmpUpdateFailed(OmpFailureKind.IntegrityFailed, "2.0.0", "The download does not match its digest."));
                    }

                    installed = true;
                    return Task.FromResult<OmpUpdateResult>(new OmpUpdated("2.0.0", null, "Omp 2.0.0 is active."));
                },
            };
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(Services(_ => Task.FromResult(Status(installed)), managed));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp is not installed. " + Guidance, StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, InstallName));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp could not be installed: The download does not match its digest.", StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, RetryName));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp 2.0.0 from the managed install. Omp 2.0.0 is now the managed install.", StringComparer.Ordinal)).ConfigureAwait(true);
            managed.Installs.Count.ShouldBe(2);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Cancel stops a running install and returns the card to the status it had before.</summary>
    [Fact]
    public async Task CancellingAnInstallReturnsTheCardToItsStatus()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var managed = new FakeOmpManagedInstall
            {
                ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
                Install = async (_, cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                    return new OmpUpdated("2.0.0", null, "unreachable");
                },
            };
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(Services(_ => Task.FromResult(Status(installed: false)), managed));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp is not installed. " + Guidance, StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, InstallName));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Installing Omp 2.0.0.", StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, CancelName));
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp is not installed. " + Guidance, StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A registry that cannot be reached after a start shows the installed version with the time it was last checked.</summary>
    [Fact]
    public async Task AnOfflineStartShowsTheInstalledVersionWithTheLastCheckedTime()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var managed = new FakeOmpManagedInstall
            {
                Check = _ => Task.FromResult<OmpUpdateResult>(new OmpOffline("1.2.3", CheckedAt, "The registry could not be reached.")),
            };
            var installer = new LocalOmpInstaller(managed);
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(Services(_ => Task.FromResult(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Managed)), managed, installer.CheckAsync));

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains(
                "Omp 1.2.3 from the managed install. Could not reach the registry to check for a newer Omp. Last checked 2026-11-07 12:00 UTC.",
                StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A newer omp found after the window is shown is installed, and the card names the new version.</summary>
    [Fact]
    public async Task AnUpdateFoundAfterStartIsInstalledAndTheCardNamesIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var updated = false;
            var managed = new FakeOmpManagedInstall
            {
                Check = _ =>
                {
                    updated = true;
                    return Task.FromResult<OmpUpdateResult>(new OmpUpdated("2.0.0", "1.2.3", "Omp 2.0.0 is active."));
                },
            };
            var installer = new LocalOmpInstaller(managed);
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(Services(_ => Task.FromResult(LocalOmpStatus.Installed(updated ? "2.0.0" : "1.2.3", LocalOmpSource.Managed)), managed, installer.CheckAsync));

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp 2.0.0 from the managed install. Omp 2.0.0 is now the managed install.", StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The status is shown while the update check is still running, so the check never delays the window.</summary>
    [Fact]
    public async Task TheStatusIsShownWhileTheUpdateCheckIsStillRunning()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var checkStarted = false;
            var managed = new FakeOmpManagedInstall();
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices
            {
                LocalOmp = _ => Task.FromResult(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Managed)),
                LocalOmpUpdateCheck = async cancellationToken =>
                {
                    checkStarted = true;
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                    return LocalOmpUpdate.None;
                },
                LocalOmpInstall = (progress, cancellationToken) => new LocalOmpInstaller(managed).InstallAsync(progress, cancellationToken),
            });

            await WaitForAsync(fixture, () => checkStarted && ShellTree.VisibleTexts(main).Contains("Omp 1.2.3 from the managed install.", StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    private static LocalOmpStatus Status(bool installed) =>
        installed ? LocalOmpStatus.Installed("2.0.0", LocalOmpSource.Managed) : LocalOmpStatus.NotInstalled(Guidance);

    private static ShellServices Services(
        Func<CancellationToken, Task<LocalOmpStatus>> status,
        FakeOmpManagedInstall managed,
        Func<CancellationToken, Task<LocalOmpUpdate>>? check = null)
    {
        var installer = new LocalOmpInstaller(managed);
        return new ShellServices
        {
            LocalOmp = status,
            LocalOmpUpdateCheck = check,
            LocalOmpInstall = (progress, cancellationToken) => installer.InstallAsync(progress, cancellationToken),
        };
    }

    private static Button ButtonNamed(ShellFixture fixture, string name) =>
        fixture.ServicesMain!.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    private static async Task WaitForAsync(ShellFixture fixture, Func<bool> condition) =>
        await ShellWait.UntilAsync(fixture.ServicesWindow!, condition, "the landing shows the change", Token).ConfigureAwait(true);
}
