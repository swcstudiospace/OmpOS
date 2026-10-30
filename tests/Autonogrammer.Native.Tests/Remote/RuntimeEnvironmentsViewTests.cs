using System.ComponentModel;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Remote;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Presentation.Settings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>
/// The Containers and WSL section under the ssh list of the Remote page, on the headless desktop: what it shows, that the running containers podman lists can be added, that the forms save the
/// profiles of their kind, that WSL is offered only where it is supported, and that every control has a name and a target of the minimum size. The host and the lister are scripted.
/// </summary>
public sealed class RuntimeEnvironmentsViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RemoteEnvironmentsSettingsPage Page(ScriptedRemoteHost remote, ScriptedContainerHost containers, ScriptedContainerLister lister) =>
        new(new RemoteEnvironmentsViewModel(remote), new RuntimeEnvironmentsViewModel(remote, containers, lister));

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
        root.GetVisualDescendants().OfType<Button>().First(button => button.IsEffectivelyVisible && string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static List<string> Shown(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text!)];

    private static EnvironmentProfile Saved(string container) => new()
    {
        Id = "container-" + container,
        DisplayName = container,
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = container, WorkspaceFolder = "/workspaces/" + container },
    };

    /// <summary>With nothing saved the section says so, offers the three add buttons (WSL where supported), names every control and keeps every target at the minimum size; podman is not asked.</summary>
    [Fact]
    public async Task TheEmptySectionOffersTheAddButtonsAndAsksNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var lister = new ScriptedContainerLister();
            var page = Page(new ScriptedRemoteHost(), new ScriptedContainerHost(), lister);
            var view = new RemoteEnvironmentsPageView { DataContext = page };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(page.Runtime!, () => page.Runtime!.IsEmpty).ConfigureAwait(true);
            ShellHarness.Settle(window);

            var section = view.RuntimeSection.ShouldNotBeNull();
            Shown(view).ShouldContain(ContainerCopy.Title, StringComparer.Ordinal);
            Shown(view).ShouldContain(ContainerCopy.EmptyText, StringComparer.Ordinal);
            section.AddFromRunning.IsEffectivelyVisible.ShouldBeTrue();
            section.AddContainer.IsEffectivelyVisible.ShouldBeTrue();
            section.AddWsl.IsEffectivelyVisible.ShouldBeTrue();
            section.WslNote.IsEffectivelyVisible.ShouldBeFalse();
            section.Picker.IsVisible.ShouldBeFalse("the list of running containers opens on request");
            lister.Calls.ShouldBe(0);
            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();
            foreach (var button in section.GetVisualDescendants().OfType<Button>().Where(static button => button.IsEffectivelyVisible))
            {
                button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Where WSL is not supported the add button is gone and the section says why.</summary>
    [Fact]
    public async Task WithoutWslSupportTheWslButtonIsReplacedByANote()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var page = Page(new ScriptedRemoteHost(), new ScriptedContainerHost { Wsl = false }, new ScriptedContainerLister());
            var view = new RemoteEnvironmentsPageView { DataContext = page };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(page.Runtime!, () => page.Runtime!.IsEmpty).ConfigureAwait(true);
            ShellHarness.Settle(window);

            var section = view.RuntimeSection.ShouldNotBeNull();
            section.AddWsl.IsEffectivelyVisible.ShouldBeFalse();
            section.WslNote.IsEffectivelyVisible.ShouldBeTrue();
            section.WslNote.Text.ShouldBe(ContainerCopy.WslWindowsOnly);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A running container from podman's list is added: the list opens on request, the chosen container fills the form (with the dev container's folder), and Save stores a container profile and shows its row.</summary>
    [Fact]
    public async Task ARunningContainerIsAddedFromThePicker()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var remote = new ScriptedRemoteHost();
            var lister = new ScriptedContainerLister { Containers = [new ContainerSummary("c1", "api", "docker.io/library/node:22", "running", "/home/me/api")] };
            var page = Page(remote, new ScriptedContainerHost(), lister);
            var view = new RemoteEnvironmentsPageView { DataContext = page };
            var window = ViewHost.Show(view);
            var runtime = page.Runtime!;
            await WaitUntilAsync(runtime, () => runtime.IsEmpty).ConfigureAwait(true);
            var section = view.RuntimeSection.ShouldNotBeNull();

            Click(section.AddFromRunning);
            await WaitUntilAsync(runtime.Picker, () => runtime.Picker.IsReady).ConfigureAwait(true);
            ShellHarness.Settle(window);
            section.Picker.IsEffectivelyVisible.ShouldBeTrue();
            section.PickerItems.Items.Count.ShouldBe(1);
            section.PickerAdd.IsEnabled.ShouldBeFalse("nothing is chosen yet");
            section.PickerItems.SelectedIndex = 0;
            ShellHarness.Settle(window);
            section.PickerAdd.IsEnabled.ShouldBeTrue();
            Click(section.PickerAdd);
            ShellHarness.Settle(window);

            section.EditorFrame.IsEffectivelyVisible.ShouldBeTrue();
            var name = section.GetVisualDescendants().OfType<TextBox>().First(box => string.Equals(AutomationProperties.GetName(box), ContainerCopy.ContainerNameLabel, StringComparison.Ordinal));
            name.Text.ShouldBe("api");
            var folder = section.GetVisualDescendants().OfType<TextBox>().First(box => string.Equals(AutomationProperties.GetName(box), ContainerCopy.ContainerFolderLabel, StringComparison.Ordinal));
            folder.Text.ShouldBe("/workspaces/api");
            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();
            Click(section.Save);
            await WaitUntilAsync(runtime, () => runtime.Rows.Count == 1 && !runtime.IsEditing).ConfigureAwait(true);
            ShellHarness.Settle(window);

            var stored = remote.Profiles.Single().Profile;
            stored.Kind.ShouldBe(EnvironmentKind.Container);
            stored.Container!.Container.ShouldBe("api");
            stored.Container.WorkspaceFolder.ShouldBe("/workspaces/api");
            Shown(view).ShouldContain("api", StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A form with a wrong folder shows the problem and Save stores nothing; a WSL distribution is added through its own form.</summary>
    [Fact]
    public async Task TheFormsShowTheirProblemsAndTheWslFormSavesAWslProfile()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var remote = new ScriptedRemoteHost();
            var page = Page(remote, new ScriptedContainerHost(), new ScriptedContainerLister());
            var view = new RemoteEnvironmentsPageView { DataContext = page };
            var window = ViewHost.Show(view);
            var runtime = page.Runtime!;
            await WaitUntilAsync(runtime, () => runtime.IsEmpty).ConfigureAwait(true);
            var section = view.RuntimeSection.ShouldNotBeNull();

            Click(section.AddContainer);
            ShellHarness.Settle(window);
            runtime.ContainerEditor.ShouldNotBeNull().IsValid.ShouldBeFalse("an empty form has problems");
            Click(section.Save);
            ShellHarness.Settle(window);
            remote.Profiles.ShouldBeEmpty();
            runtime.ContainerEditor.ShouldNotBeNull().Issues.ShouldNotBeEmpty();
            Click(ButtonNamed(section, RemoteCopy.CancelLabel));
            ShellHarness.Settle(window);
            section.EditorFrame.IsEffectivelyVisible.ShouldBeFalse();

            Click(section.AddWsl);
            ShellHarness.Settle(window);
            var wsl = runtime.WslEditor.ShouldNotBeNull();
            wsl.DisplayName = "Ubuntu";
            wsl.Distro = "Ubuntu";
            wsl.WorkspacePath = "/home/dev/app";
            Click(section.Save);
            await WaitUntilAsync(runtime, () => runtime.Rows.Count == 1 && !runtime.IsEditing).ConfigureAwait(true);

            var stored = remote.Profiles.Single().Profile;
            stored.Kind.ShouldBe(EnvironmentKind.Wsl);
            stored.Wsl!.Distro.ShouldBe("Ubuntu");
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Test on a saved container row asks the host for that container and shows the outcome in words on the row.</summary>
    [Fact]
    public async Task TestingARowAsksTheHostAndShowsTheOutcome()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var remote = new ScriptedRemoteHost();
            await remote.SaveProfileAsync(Saved("api"), Token).ConfigureAwait(true);
            var containers = new ScriptedContainerHost();
            var page = Page(remote, containers, new ScriptedContainerLister());
            var view = new RemoteEnvironmentsPageView { DataContext = page };
            var window = ViewHost.Show(view);
            var runtime = page.Runtime!;
            await WaitUntilAsync(runtime, () => runtime.IsReady).ConfigureAwait(true);
            ShellHarness.Settle(window);
            var section = view.RuntimeSection.ShouldNotBeNull();

            Click(ButtonNamed(section, RemoteCopy.TestLabel));
            await WaitUntilAsync(runtime.Rows[0], () => runtime.Rows[0].IsGood).ConfigureAwait(true);
            ShellHarness.Settle(window);

            containers.Tested.ShouldBe(["api"]);
            Shown(section).ShouldContain(runtime.Rows[0].StatusLabel, StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }
}
