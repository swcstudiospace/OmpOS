using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Landing;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>
/// An install that does not replace the managed omp keeps the card on the omp it has, with the same notice the update check shows for that outcome.
/// Only a failed install shows the install failure. The managed install is a fake; nothing reaches the network or starts an omp.
/// </summary>
public sealed class CodeLandingInstallOutcomeShellTests
{
    private const string OmpLine = "Omp 1.0.0 from PATH.";
    private const string InstallName = CodeLandingCopy.InstallOmp + ", " + CodeLandingCopy.LocalTitle;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A deferred install, because the omp in use cannot be replaced, shows the deferred notice on the omp it kept.</summary>
    [Fact]
    public async Task ADeferredInstallShowsTheDeferredNoticeOnTheKeptOmp()
    {
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
            Install = (_, _) => Task.FromResult<OmpUpdateResult>(new OmpDeferred("2.1.0", "In use.")),
        };

        await ShowsNoticeAfterInstallAsync(managed, OmpLine + " " + CodeLandingCopy.UpdateDeferred);
    }

    /// <summary>A rolled-back install shows the rollback notice with its reason on the omp that was restored.</summary>
    [Fact]
    public async Task ARolledBackInstallShowsTheRollbackNoticeOnTheRestoredOmp()
    {
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => Task.FromResult(FakeOmpManagedInstall.ReleaseOf("2.0.0")),
            Install = (_, _) => Task.FromResult<OmpUpdateResult>(new OmpRolledBack("2.1.0", "1.0.0", "Rolled back.")),
        };

        await ShowsNoticeAfterInstallAsync(managed, OmpLine + " A newer Omp failed its check and was rolled back. Rolled back.");
    }

    /// <summary>An install whose release cannot be read because the registry is unreachable shows the offline notice on the omp it kept.</summary>
    [Fact]
    public async Task AnOfflineInstallShowsTheOfflineNoticeOnTheKeptOmp()
    {
        var managed = new FakeOmpManagedInstall
        {
            ReadRelease = _ => throw new HttpRequestException("The registry is unreachable."),
        };

        await ShowsNoticeAfterInstallAsync(managed, OmpLine + " " + CodeLandingCopy.OfflineNeverChecked);
    }

    private static async Task ShowsNoticeAfterInstallAsync(FakeOmpManagedInstall managed, string expected)
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(false);
        await headless.RunAsync(async () =>
        {
            var installer = new LocalOmpInstaller(managed);
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices
            {
                LocalOmp = _ => Task.FromResult(LocalOmpStatus.Missing("1.0.0", LocalOmpSource.Path)),
                LocalOmpInstall = (progress, cancellationToken) => installer.InstallAsync(progress, cancellationToken),
            });
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains(OmpLine, StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, InstallName));

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains(expected, StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    private static Button ButtonNamed(ShellFixture fixture, string name) =>
        fixture.ServicesMain!.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    private static async Task WaitForAsync(ShellFixture fixture, Func<bool> condition) =>
        await ShellWait.UntilAsync(fixture.ServicesWindow!, condition, "the landing shows the change", Token).ConfigureAwait(true);
}
