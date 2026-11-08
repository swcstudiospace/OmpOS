using System.ComponentModel;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Remote;
using Autonogrammer.Presentation.Remote;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>
/// The Omp buttons of the Remote page on the headless desktop: each is named, Check Omp is pressable on a saved environment, and Install Omp is enabled only
/// after a check that planned lines, with those lines shown on the page.
/// </summary>
public sealed class RemoteOmpViewTests
{
    private const string PlannedLine = "Install Omp 18.18.1 into /home/tester/.local/bin/omp (built with Bun 1.4.4)";

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

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static List<string> Shown(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text!)];

    /// <summary>Check Omp is enabled and Install Omp is not until a check planned lines; after the check the planned line is on the page and Install Omp is enabled.</summary>
    [Fact]
    public async Task InstallOmpWaitsForACheckThatPlannedLines()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new ScriptedRemoteHost().With("a", "alpha");
            var omp = new ScriptedRemoteOmp(new RemoteOmpReport(RemoteOmpOutcome.Planned, [], [PlannedLine], null, false));
            var model = new RemoteEnvironmentsViewModel(host, omp);
            var view = new RemoteEnvironmentsPageView { DataContext = model };
            var window = ViewHost.Show(view);
            await WaitUntilAsync(model, () => model.IsReady).ConfigureAwait(true);
            ShellHarness.Settle(window);

            var check = ButtonNamed(view, RemoteCopy.CheckOmpLabel);
            var install = ButtonNamed(view, RemoteCopy.InstallOmpLabel);
            check.IsEnabled.ShouldBeTrue();
            install.IsEnabled.ShouldBeFalse();

            Click(check);
            await WaitUntilAsync(model.Rows[0], () => model.Rows[0].HasOmpResult).ConfigureAwait(true);
            ShellHarness.Settle(window);

            install.IsEnabled.ShouldBeTrue();
            Shown(view).ShouldContain(PlannedLine, StringComparer.Ordinal);
            Shown(view).ShouldContain(RemoteCopy.OmpPlanned, StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A head without an Omp provisioner shows no Omp buttons at all.</summary>
    [Fact]
    public async Task AHeadWithoutAProvisionerShowsNoOmpButtons()
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

            view.GetVisualDescendants().OfType<Button>().Count(button => string.Equals(AutomationProperties.GetName(button), RemoteCopy.CheckOmpLabel, StringComparison.Ordinal) && button.IsEffectivelyVisible).ShouldBe(0);
            window.Close();
        }, Token).ConfigureAwait(true);
    }
}
