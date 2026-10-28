using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>The ways into the Settings window (Ctrl+, and the rail button) over a headless main window, and what the head connects to them.</summary>
public sealed class SettingsEntryTests
{
    /// <summary>Ctrl+, asks for the Settings window, and so does it from inside the prompt.</summary>
    [Fact]
    public async Task CtrlCommaRequestsTheSettingsWindowFromAnywhere()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var raised = 0;
            main.SettingsRequested += (_, _) => raised++;

            ShellHarness.Press(window, Key.OemComma, PhysicalKey.Comma, control: true);
            raised.ShouldBe(1);

            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.OemComma, PhysicalKey.Comma, control: true);
            raised.ShouldBe(2);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A comma alone, or with Shift, is not the shortcut: it is typed text.</summary>
    [Fact]
    public async Task OtherCommaKeysDoNotRequestSettings()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var raised = 0;
            main.SettingsRequested += (_, _) => raised++;

            ShellHarness.Press(window, Key.OemComma, PhysicalKey.Comma);
            ShellHarness.Press(window, Key.OemComma, PhysicalKey.Comma, shift: true, control: true);

            raised.ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The rail footer has a Settings button, named, with the shortcut in its tooltip, in both views of the rail. Pressing it asks for the window.</summary>
    [Fact]
    public async Task TheRailButtonRequestsSettingsInBothViews()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var button = main.FindControl<Button>("SettingsButton").ShouldNotBeNull();
            var raised = 0;
            main.SettingsRequested += (_, _) => raised++;

            AutomationProperties.GetName(button).ShouldBe("Settings");
            ToolTip.GetTip(button).ShouldBe(MainView.SettingsTooltip);
            button.IsEffectivelyVisible.ShouldBeTrue();
            ShellHarness.RaiseClick(button);
            raised.ShouldBe(1);

            ShellHarness.ShowToolsView(window);
            button.IsEffectivelyVisible.ShouldBeTrue("the footer stays while the rail shows the tool toggles");
            ShellHarness.RaiseClick(button);
            raised.ShouldBe(2);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With the head attached, Ctrl+, opens the window over the main window once; asking again brings the same window forward; closing the main window closes it.</summary>
    [Fact]
    public async Task AttachedSettingsOpenOneWindowOverTheMainWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var windows = new List<FakeSettingsWindow>();
        var engines = NewEngines(tree);
        var settings = HeadSettings.Create(
            engines,
            new FileSettingsStore(tree.State),
            TimeProvider.System,
            dispatch: null,
            new HeadSettingsOptions
            {
                GetVariable = static _ => null,
                WindowFactory = model =>
                {
                    var fake = new FakeSettingsWindow(model);
                    windows.Add(fake);
                    return fake;
                },
            });
        try
        {
            await using var session = TestAppBuilder.StartSession();
            await session.RunAsync(async () =>
            {
                var window = ShellHarness.Show();
                settings.Attach(window);

                ShellHarness.Press(window, Key.OemComma, PhysicalKey.Comma, control: true);
                await WaitForAsync(() => windows.Count == 1 && windows[0].ShowCount == 1, ct).ConfigureAwait(true);
                windows[0].Owner.ShouldBeSameAs(window);

                ShellHarness.RaiseClick(ShellHarness.MainViewOf(window).FindControl<Button>("SettingsButton")!);
                await WaitForAsync(() => windows[0].ActivateCount == 1, ct).ConfigureAwait(true);
                windows.Count.ShouldBe(1);

                window.Close();
                windows[0].ClosedForShutdown.ShouldBeTrue();
                settings.Host.IsOpen.ShouldBeFalse();
            }, ct).ConfigureAwait(true);
        }
        finally
        {
            await settings.DisposeAsync().ConfigureAwait(true);
            await engines.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The saved auto-run choice is what the Terminal tool reads: the provider follows the file at every call.</summary>
    [Fact]
    public async Task TheTerminalToolReadsTheSavedAutoRunChoice()
    {
        using var tree = new TempTree();
        var engines = NewEngines(tree);
        var store = new FileSettingsStore(tree.State);
        var settings = HeadSettings.Create(engines, store, TimeProvider.System, dispatch: null, new HeadSettingsOptions { GetVariable = static _ => null });
        try
        {
            await using var session = TestAppBuilder.StartSession();
            await session.Dispatch(() =>
            {
                var window = ShellHarness.Show();
                settings.Attach(window);
                var provider = ShellHarness.MainViewOf(window).TerminalSettingsProvider.ShouldNotBeNull();
                provider().AutoRunEnabled.ShouldBeFalse();

                store.SetTerminalAutoRun(autoRun: true);
                provider().AutoRunEnabled.ShouldBeTrue();

                store.SetTerminalAutoRun(autoRun: false);
                provider().AutoRunEnabled.ShouldBeFalse();
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await settings.DisposeAsync().ConfigureAwait(true);
            await engines.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A saved motion choice reaches the shell at once: the reduced-motion class goes on and comes off both the window and the main view.</summary>
    [Fact]
    public async Task ASavedMotionChoiceIsAppliedToTheShell()
    {
        using var tree = new TempTree();
        var engines = NewEngines(tree);
        var store = new FileSettingsStore(tree.State);
        var settings = HeadSettings.Create(engines, store, TimeProvider.System, dispatch: null, new HeadSettingsOptions { GetVariable = static _ => null });
        try
        {
            await using var session = TestAppBuilder.StartSession();
            await session.Dispatch(() =>
            {
                var window = ShellHarness.Show();
                var main = ShellHarness.MainViewOf(window);
                settings.Attach(window);
                window.Classes.Contains(MainView.ReducedMotionClass).ShouldBeFalse();

                store.SetMotion(MotionPreference.Reduced);
                window.Classes.Contains(MainView.ReducedMotionClass).ShouldBeTrue();
                main.Classes.Contains(MainView.ReducedMotionClass).ShouldBeTrue();
                (main.FindControl<TabStrip>("TopTabs")!.Transitions?.Count ?? 0).ShouldBe(0);

                store.SetMotion(MotionPreference.Full);
                window.Classes.Contains(MainView.ReducedMotionClass).ShouldBeFalse();
                main.Classes.Contains(MainView.ReducedMotionClass).ShouldBeFalse();
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await settings.DisposeAsync().ConfigureAwait(true);
            await engines.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A saved Runtime window reaches the sweeper of the head at once.</summary>
    [Fact]
    public async Task ASavedRuntimeWindowReachesTheSweeper()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(new AgentHeadOptions
        {
            PluginsRoot = tree.Plugins,
            StateRoot = tree.State,
            WorkspaceRoot = tree.Workspace,
            SelectMostRecentSession = false,
            MaintenanceInterval = Timeout.InfiniteTimeSpan,
            DetailsRefreshInterval = Timeout.InfiniteTimeSpan,
            RuntimeSweepInterval = Timeout.InfiniteTimeSpan,
        });
        await using var headLifetime = head.ConfigureAwait(true);
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            head.Settings.Attach(window);

            head.Settings.Store.SetRuntime(new RuntimeSettingsValues(IdleStopHours: 3, RetentionDays: 2));

            head.Settings.Sweeper.ShouldNotBeNull().Current.ShouldBe(new RuntimeSettingsValues(3, 2));
        }, ct).ConfigureAwait(true);
    }

    private static SessionEngineSet NewEngines(TempTree tree) => new(new SessionEngineSetOptions
    {
        Loop = AgentLoopEnvironment.Resolve(static _ => null, tree.Workspace, tree.Workspace, tree.Root),
        AllowedKinds = [],
    });

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var limit = TimeProvider.System.GetUtcNow().AddSeconds(10);
        while (!condition())
        {
            if (TimeProvider.System.GetUtcNow() > limit)
            {
                throw new TimeoutException("The condition was not met within ten seconds.");
            }

            await Task.Yield();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
