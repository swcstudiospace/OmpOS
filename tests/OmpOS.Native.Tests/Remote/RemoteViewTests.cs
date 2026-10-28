using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Contracts.Environments;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Native.Views.Remote;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Remote;

/// <summary>
/// The Remote environments page and the New session environment picker on the headless desktop: what they show for each state, that every action reaches the
/// model, that every control has an accessible name and a target of at least the minimum size, and that the status is said in words.
/// </summary>
public sealed class RemoteViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task WaitUntilAsync(INotifyPropertyChanged source, Func<bool> condition)
    {
        if (condition())
        {
            return;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            if (condition())
            {
                done.TrySetResult();
            }
        }

        source.PropertyChanged += Handler;
        try
        {
            await done.Task.WaitAsync(TimeSpan.FromSeconds(20), Token).ConfigureAwait(true);
        }
        finally
        {
            source.PropertyChanged -= Handler;
        }
    }

    private static Button ButtonNamed(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    private static IReadOnlyList<Button> VisibleButtons(Control root, string name) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal))];

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static List<string> Shown(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text!)];

    /// <summary>With nothing saved the page shows the empty state, offers the hosts of the ssh config and the hand-entry button, and contacts no host.</summary>
    [Fact]
    public async Task TheEmptyPageOffersTheConfigHostsAndContactsNoHost()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new ScriptedRemoteHost { ConfigAliases = ["alpha", "beta"] };
            var model = new RemoteEnvironmentsViewModel(host);
            var view = new RemoteEnvironmentsPageView { DataContext = model };
            var window = ViewHost.Show(view);

            await WaitUntilAsync(model, () => model.IsEmpty).ConfigureAwait(true);
            ShellHarness.Settle(window);

            view.Empty.IsEffectivelyVisible.ShouldBeTrue();
            view.Loading.IsEffectivelyVisible.ShouldBeFalse();
            Shown(view).ShouldContain(RemoteCopy.EmptyHeading, StringComparer.Ordinal);
            Shown(view).ShouldContain("alpha", StringComparer.Ordinal);
            Shown(view).ShouldContain("beta", StringComparer.Ordinal);
            VisibleButtons(view, RemoteCopy.AddFromConfigLabel).Count.ShouldBe(2);
            view.AddManual.IsEffectivelyVisible.ShouldBeTrue();
            view.AddManual.IsEnabled.ShouldBeTrue();
            host.TestCalls.ShouldBe(0);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A saved environment is a card with its name, destination, folder and last-used line; Test connection shows the outcome in words and the omp version found.</summary>
    [Fact]
    public async Task TestConnectionShowsTheOutcomeInWords()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new ScriptedRemoteHost().With("a", "alpha");
            var model = new RemoteEnvironmentsViewModel(host);
            var view = new RemoteEnvironmentsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.IsReady).ConfigureAwait(true);
            ShellHarness.Settle(window);

            Shown(view).ShouldContain("Box a", StringComparer.Ordinal);
            Shown(view).ShouldContain("alpha", StringComparer.Ordinal);
            Shown(view).ShouldContain(RemoteCopy.NeverUsed, StringComparer.Ordinal);
            Shown(view).ShouldContain("Not checked", StringComparer.Ordinal);
            Click(ButtonNamed(view, RemoteCopy.TestLabel));
            await WaitUntilAsync(model.Rows[0], () => model.Rows[0].Status == RemoteEnvironmentStatus.Ready).ConfigureAwait(true);
            ShellHarness.Settle(window);

            host.TestCalls.ShouldBe(1);
            Shown(view).ShouldContain("Ready", StringComparer.Ordinal);
            Shown(view).ShouldContain("omp 18.12.6, Linux x86_64", StringComparer.Ordinal);
            Shown(view).ShouldNotContain("Not checked", StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A failed test shows its findings: severity in words, the problem and how to fix it; an unknown host key offers the trust terminal and pressing it reaches the host.</summary>
    [Fact]
    public async Task AFailedTestShowsTheFindingsAndTheTrustTerminalButton()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new ScriptedRemoteHost
            {
                Report = new RemotePreflightReport
                {
                    Succeeded = false,
                    Diagnoses = [new RemoteDiagnosis { Kind = RemoteDiagnosisKind.HostKeyUnknown, Severity = DiagnosisSeverity.Error, Summary = "The host key is not trusted yet.", FixText = "Open the host trust terminal." }],
                },
            }.With("a", "alpha");
            var model = new RemoteEnvironmentsViewModel(host);
            var view = new RemoteEnvironmentsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.IsReady).ConfigureAwait(true);
            VisibleButtons(view, RemoteCopy.TrustHostLabel).ShouldBeEmpty();

            Click(ButtonNamed(view, RemoteCopy.TestLabel));
            await WaitUntilAsync(model.Rows[0], () => model.Rows[0].Status == RemoteEnvironmentStatus.HostKeyUnconfirmed).ConfigureAwait(true);
            ShellHarness.Settle(window);

            Shown(view).ShouldContain("Host key not trusted yet", StringComparer.Ordinal);
            Shown(view).ShouldContain(RemoteCopy.ErrorLabel, StringComparer.Ordinal);
            Shown(view).ShouldContain("The host key is not trusted yet.", StringComparer.Ordinal);
            Shown(view).ShouldContain("Open the host trust terminal.", StringComparer.Ordinal);
            var trust = VisibleButtons(view, RemoteCopy.TrustHostLabel).ShouldHaveSingleItem();
            Click(trust);
            await WaitUntilAsync(model, () => string.Equals(model.Announcement, RemoteCopy.TrustHostOpened, StringComparison.Ordinal)).ConfigureAwait(true);

            host.TrustRequests.ShouldHaveSingleItem().Alias.ShouldBe("alpha");
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Add by hand opens the form with focus in its first field, Save stays disabled while a field has a problem, and a valid form saves the environment and closes.</summary>
    [Fact]
    public async Task TheFormOpensWithFocusValidatesAndSaves()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new ScriptedRemoteHost();
            var model = new RemoteEnvironmentsViewModel(host);
            var view = new RemoteEnvironmentsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.IsEmpty).ConfigureAwait(true);

            Click(view.AddManual);
            ShellHarness.Settle(window);

            model.IsEditing.ShouldBeTrue();
            view.EditorFrame.IsEffectivelyVisible.ShouldBeTrue();
            var name = view.GetVisualDescendants().OfType<TextBox>().First(box => string.Equals(box.Name, "DisplayNameBox", StringComparison.Ordinal));
            var alias = view.GetVisualDescendants().OfType<TextBox>().First(box => string.Equals(box.Name, "AliasBox", StringComparison.Ordinal));
            var root = view.GetVisualDescendants().OfType<TextBox>().First(box => string.Equals(box.Name, "RootBox", StringComparison.Ordinal));
            var save = ButtonNamed(view, RemoteCopy.SaveLabel);
            save.IsEnabled.ShouldBeFalse();
            (TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement()).ShouldBeSameAs(name);

            name.Text = "Build box";
            alias.Text = "build-box";
            root.Text = "relative/path";
            ShellHarness.Settle(window);
            save.IsEnabled.ShouldBeFalse();
            Shown(view).ShouldContain("The remote workspace root must be an absolute POSIX path without control characters or '..' segments.", StringComparer.Ordinal);

            root.Text = "/srv/app";
            ShellHarness.Settle(window);
            save.IsEnabled.ShouldBeTrue();
            Click(save);
            await WaitUntilAsync(model, () => model.Rows.Count == 1).ConfigureAwait(true);
            ShellHarness.Settle(window);

            host.Profiles.ShouldHaveSingleItem().Profile.Ssh!.Alias.ShouldBe("build-box");
            model.IsEditing.ShouldBeFalse();
            view.EditorFrame.IsEffectivelyVisible.ShouldBeFalse();
            Shown(view).ShouldContain("Build box", StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every keyboard stop of the page has an accessible name, every visible button is at least the minimum target, and the announcement is a polite live region.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnoughAndTheAnnouncementIsLive()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new ScriptedRemoteHost { ConfigAliases = ["beta"] }.With("a", "alpha");
            var model = new RemoteEnvironmentsViewModel(host);
            var view = new RemoteEnvironmentsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.IsReady).ConfigureAwait(true);
            Click(view.AddManual);
            ShellHarness.Settle(window);

            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();
            var buttons = view.GetVisualDescendants().OfType<Button>().Where(button => button.TemplatedParent is null && button.IsEffectivelyVisible).ToList();
            buttons.Count.ShouldBeGreaterThan(5);
            foreach (var button in buttons)
            {
                button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            AutomationProperties.GetLiveSetting(view.Announcement).ShouldBe(AutomationLiveSetting.Polite);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The picker lists this computer, each environment and the cloud; choosing reaches the model; a blocked environment shows its reason; Manage raises the event.</summary>
    [Fact]
    public async Task ThePickerChoosesValidatesAndRaisesManage()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new NewSessionTargetModel(
            [
                new EnvironmentProfile { Id = "a", DisplayName = "Box a", Kind = EnvironmentKind.Ssh, Ssh = new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/srv/app" } },
            ]);
            var view = new EnvironmentPickerView { DataContext = model };
            var window = ViewHost.Show(view);
            var managed = 0;
            view.ManageRequested += (_, _) => managed++;

            view.Targets.ItemCount.ShouldBe(3);
            view.Targets.SelectedItem.ShouldBeSameAs(model.Options[0]);
            view.ValidationMessage.Text.ShouldBe(RemoteCopy.TargetOk);

            view.Targets.SelectedItem = model.Options[2];
            ShellHarness.Settle(window);
            model.Selected!.Id.ShouldBe("cloud");
            view.ValidationMessage.Text.ShouldBe(RemoteCopy.CloudUnavailable);

            view.Targets.SelectedItem = model.Options[1];
            model.SetStatus("a", RemoteEnvironmentStatus.AuthFailed);
            ShellHarness.Settle(window);
            model.Selected.Id.ShouldBe("ssh:a");
            view.ValidationMessage.Text.ShouldStartWith(RemoteCopy.TargetBlockedPrefix, Case.Sensitive);
            Shown(view).ShouldContain(RemoteStatusDescriber.LabelOf(RemoteEnvironmentStatus.AuthFailed), StringComparer.Ordinal);

            Click(view.Manage);
            managed.ShouldBe(1);
            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();
            AutomationProperties.GetLiveSetting(view.ValidationMessage).ShouldBe(AutomationLiveSetting.Polite);
            window.Close();
        }, Token).ConfigureAwait(true);
    }
}
