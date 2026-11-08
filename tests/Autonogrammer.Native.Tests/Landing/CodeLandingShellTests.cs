using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Native.Tests.Account;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Remote;
using Autonogrammer.Native.Views.Landing;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.Presentation.Landing;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Presentation.Settings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>The Code landing in the shell: shown only while no session is selected, its cards wired to the picker and the Settings route, and the omp status each source gets.</summary>
public sealed class CodeLandingShellTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The landing shows while no session is selected and is gone once a session is selected.</summary>
    [Fact]
    public async Task TheLandingShowsOnlyWhileNoSessionIsSelected()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var empty = ShellFixture.Create();
            await using var emptyScope = empty.ConfigureAwait(true);
            var emptyMain = empty.ShowWithServices(new ShellServices());
            Landing(emptyMain).IsEffectivelyVisible.ShouldBeTrue();

            var withSession = ShellFixture.CreateWithSession();
            await using var sessionScope = withSession.ConfigureAwait(true);
            var sessionMain = withSession.ShowWithServices(new ShellServices());
            (sessionMain.FindControl<ContentControl>("TranscriptHost")!.Content is CodeLandingView).ShouldBeFalse("a selected session owns the thread, so the landing is not shown");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Activating this computer's card starts a new agent on the local environment: the picker is set to it and a session is created.</summary>
    [Fact]
    public async Task ActivatingLocalStartsAnAgentOnTheLocalEnvironment()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var pending = new PendingSessionTarget();
            var host = new ScriptedRemoteHost().With("box-1", "alpha");
            var main = fixture.ShowWithServices(new ShellServices
            {
                RemoteEnvironments = host,
                NewSessionTarget = pending,
                LocalOmp = static _ => Task.FromResult(LocalOmpStatus.Installed("1.0.0", LocalOmpSource.Path)),
            });
            await WaitForAsync(fixture, () => main.TargetModel?.Options.Any(option => string.Equals(option.Id, "ssh:box-1", StringComparison.Ordinal)) == true).ConfigureAwait(true);
            main.TargetModel!.Select("ssh:box-1");

            ShellHarness.RaiseClick(CardButton(fixture, CodeLandingCopy.LocalTitle));
            await WaitForAsync(fixture, () => fixture.Panel.SessionCount == 1).ConfigureAwait(true);

            main.TargetModel!.Selected!.Id.ShouldBe(NewSessionTargetModel.LocalId);
            pending.Take().ShouldBeNull("the local environment is the default, so no ssh target is pending");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Activating a remote environment starts a new agent on that profile through the same picker and pending target.</summary>
    [Fact]
    public async Task ActivatingARemoteEnvironmentStartsAnAgentOnThatProfile()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var pending = new PendingSessionTarget();
            var host = new ScriptedRemoteHost().With("box-1", "alpha");
            var main = fixture.ShowWithServices(new ShellServices
            {
                RemoteEnvironments = host,
                NewSessionTarget = pending,
                LocalOmp = static _ => Task.FromResult(LocalOmpStatus.Installed("1.0.0", LocalOmpSource.Path)),
            });
            await WaitForAsync(fixture, () => main.TargetModel?.Options.Any(option => string.Equals(option.Id, "ssh:box-1", StringComparison.Ordinal)) == true).ConfigureAwait(true);
            var row = main.TargetModel!.Options.Single(option => string.Equals(option.Id, "ssh:box-1", StringComparison.Ordinal));

            ShellHarness.RaiseClick(ButtonNamed(fixture, row.AccessibleName));
            await WaitForAsync(fixture, () => fixture.Panel.SessionCount == 1).ConfigureAwait(true);

            main.TargetModel!.Selected!.Id.ShouldBe("ssh:box-1");
            var target = pending.Take().ShouldNotBeNull();
            target.Kind.ShouldBe(SessionTargetKind.Ssh);
            target.ProfileId.ShouldBe("box-1");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With no remote environments the Remote card says so, and its button opens the Remote page of Settings.</summary>
    [Fact]
    public async Task TheRemoteCardOpensRemoteSettingsWhenEmpty()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices
            {
                RemoteEnvironments = new ScriptedRemoteHost(),
                NewSessionTarget = new PendingSessionTarget(),
            });
            var seen = new List<SettingsSection?>();
            main.SettingsRequested += (_, e) => seen.Add(e.Section);
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains(RemoteCopy.EmptyHeading, StringComparer.Ordinal)).ConfigureAwait(true);

            ShellHarness.RaiseClick(ButtonNamed(fixture, CodeLandingCopy.ManageRemote + ", " + CodeLandingCopy.RemoteTitle));

            seen.ShouldBe([SettingsSection.Remote]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Signed out, the linked and cloud cards each say to sign in, and the sign-in button opens the sign-in window for this account.</summary>
    [Fact]
    public async Task ASignedOutAccountOffersSignInFromTheCards()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var account = new FakeAccountHost();
            var opened = new List<IAccountHost>();
            var main = fixture.ShowWithServices(new ShellServices
            {
                Account = account,
                SignIn = host => opened.Add(host),
                LocalOmp = static _ => Task.FromResult(LocalOmpStatus.Installed("1.0.0", LocalOmpSource.Path)),
            });
            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Sign in to link a machine", StringComparer.Ordinal)).ConfigureAwait(true);

            ShellTree.VisibleTexts(main).ShouldContain("Sign in to use cloud agents", StringComparer.Ordinal);
            ShellHarness.RaiseClick(ButtonNamed(fixture, CodeLandingCopy.SignIn + ", " + CodeLandingCopy.CloudTitle));

            opened.ShouldHaveSingleItem().ShouldBeSameAs(account);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Signed in and configured, the cloud card is enabled and opens the Cloud page of Settings.</summary>
    [Fact]
    public async Task ASignedInAccountEnablesCloudAndOpensItsSettings()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var account = new FakeAccountHost();
            var main = fixture.ShowWithServices(new ShellServices
            {
                Account = account,
                LocalOmp = static _ => Task.FromResult(LocalOmpStatus.Installed("1.0.0", LocalOmpSource.Path)),
            });
            var seen = new List<SettingsSection?>();
            main.SettingsRequested += (_, e) => seen.Add(e.Section);
            var cloud = CardButton(fixture, CodeLandingCopy.CloudTitle);
            cloud.IsEnabled.ShouldBeFalse();

            account.Set(new AccountHostState(AuthPhase.SignedIn, "a@example.com", false, false, false, false, false, true, null, null, 0, false, null, false));
            await WaitForAsync(fixture, () => CardButton(fixture, CodeLandingCopy.CloudTitle).IsEnabled).ConfigureAwait(true);
            ShellHarness.RaiseClick(CardButton(fixture, CodeLandingCopy.CloudTitle));

            seen.ShouldBe([SettingsSection.Cloud]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Each source of omp is said in words with its version.</summary>
    [Theory]
    [InlineData(LocalOmpSource.Managed, "Omp 2.0.0 from the managed install.")]
    [InlineData(LocalOmpSource.Configured, "Omp 2.0.0 from the configured path.")]
    [InlineData(LocalOmpSource.Path, "Omp 2.0.0 from PATH.")]
    public async Task EachOmpSourceIsSaidWithItsVersion(LocalOmpSource source, string expected)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices
            {
                LocalOmp = _ => Task.FromResult(LocalOmpStatus.Installed("2.0.0", source)),
            });

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains(expected, StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>When omp is not installed the local card says so and gives the guidance.</summary>
    [Fact]
    public async Task AMissingOmpSaysItIsNotInstalledWithTheGuidance()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices
            {
                LocalOmp = static _ => Task.FromResult(LocalOmpStatus.NotInstalled("Install omp, then put it on PATH.")),
            });

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp is not installed. Install omp, then put it on PATH.", StringComparer.Ordinal)).ConfigureAwait(true);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A check that throws shows its message and its Try again button runs the check again.</summary>
    [Fact]
    public async Task AFailedCheckShowsTheMessageAndTryAgainRunsItAgain()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var calls = 0;
            var main = fixture.ShowWithServices(new ShellServices
            {
                LocalOmp = _ =>
                {
                    calls++;
                    return calls == 1
                        ? Task.FromException<LocalOmpStatus>(new InvalidOperationException("boom"))
                        : Task.FromResult(LocalOmpStatus.Installed("1.0.0", LocalOmpSource.Path));
                },
            });

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp could not be checked: boom", StringComparer.Ordinal)).ConfigureAwait(true);
            ShellHarness.RaiseClick(ButtonNamed(fixture, CodeLandingCopy.Retry + ", " + CodeLandingCopy.LocalTitle));

            await WaitForAsync(fixture, () => ShellTree.VisibleTexts(main).Contains("Omp 1.0.0 from PATH.", StringComparer.Ordinal)).ConfigureAwait(true);
            calls.ShouldBe(2);
        }, Token).ConfigureAwait(true);
    }

    private static CodeLandingView Landing(Autonogrammer.Native.MainView main) =>
        (CodeLandingView)main.FindControl<ContentControl>("TranscriptHost")!.Content!;

    private static Button CardButton(ShellFixture fixture, string title) =>
        fixture.ServicesMain!.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => AutomationProperties.GetName(button)?.StartsWith(title + ". ", StringComparison.Ordinal) == true);

    private static Button ButtonNamed(ShellFixture fixture, string name) =>
        fixture.ServicesMain!.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    private static async Task WaitForAsync(ShellFixture fixture, Func<bool> condition) =>
        await ShellWait.UntilAsync(fixture.ServicesWindow!, condition, "the landing shows the change", Token).ConfigureAwait(true);
}
