using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Dispatch;

/// <summary>The Dispatch views draw the presentation models in their own text, announce failures assertively, and each control raises the choice it names.</summary>
public sealed class DispatchViewTests
{
    /// <summary>The page reads its devices through the Dispatch host port, the way every head does, and shows the device names it lists.</summary>
    [Fact]
    public async Task ThePageShowsTheDevicesTheFakeHostLists()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(async () =>
        {
            var host = new FakeDispatchHost([new DispatchDevice("device-1", "Office Mac", Online: true, PresenceKnown: true)]);
            var devices = await host.ListDevicesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            var view = new DispatchPageView { DataContext = new DispatchPageModel(DevicesViewModel.Loaded(devices), [], PairThisDevice: null) };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            view.GetVisualDescendants().OfType<TextBlock>().Any(text => string.Equals(text.Text, "Office Mac", StringComparison.Ordinal)).ShouldBeTrue();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An account with no devices shows the no-devices text in the page's status line.</summary>
    [Fact]
    public async Task ThePageShowsTheNoDevicesText()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var model = new DispatchPageModel(DevicesViewModel.Loaded([]), [], PairThisDevice: null);
            var view = new DispatchPageView { DataContext = model };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            view.FindControl<TextBlock>("StatusText")!.Text.ShouldBe(DispatchCopy.ForPhase(DispatchPhase.NoDevices));
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A head with no local device, the web head among them, says to pair in the desktop app.</summary>
    [Fact]
    public async Task APageWithoutALocalDeviceSaysToPairInTheDesktopApp()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var model = new DispatchPageModel(DevicesViewModel.Loaded([]), [], PairThisDevice: null);
            var view = new DispatchPageView { DataContext = model };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            view.FindControl<TextBlock>("PairText")!.Text.ShouldBe(DispatchCopy.PairInDesktopApp);
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A failed command is announced through the failure line, which is assertive.</summary>
    [Fact]
    public async Task AFailedCommandIsAnnouncedAssertively()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var row = DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Failed, "Fix the build", DispatchFailureReason.Timeout);
            var view = new DispatchPageView { DataContext = new DispatchPageModel(DevicesViewModel.Loaded([]), [row], PairThisDevice: null) };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            var failure = view.FindControl<TextBlock>("FailureText")!;
            failure.Text!.ShouldContain(DispatchCopy.ForFailure(DispatchFailureReason.Timeout));
            AutomationProperties.GetLiveSetting(failure).ShouldBe(AutomationLiveSetting.Assertive);
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An unreachable service is announced through the failure line, with the text for its phase.</summary>
    [Fact]
    public async Task AnUnreachableServiceIsAnnouncedAssertively()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var devices = DevicesViewModel.Failed(new DispatchHostException(DispatchHostFailureKind.Unavailable, "The service is down."));
            var view = new DispatchPageView { DataContext = new DispatchPageModel(devices, [], PairThisDevice: null) };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            view.FindControl<TextBlock>("FailureText")!.Text!.ShouldContain(DispatchCopy.ForPhase(DispatchPhase.Unreachable));
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An unreachable service is announced once, and the status line does not repeat it.</summary>
    [Fact]
    public async Task AnUnreachableServiceIsNotRepeatedInTheStatusLine()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var devices = DevicesViewModel.Failed(new DispatchHostException(DispatchHostFailureKind.Unavailable, "The service is down."));
            var view = new DispatchPageView { DataContext = new DispatchPageModel(devices, [], PairThisDevice: null) };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            view.FindControl<TextBlock>("StatusText")!.Text.ShouldBeEmpty();
            view.FindControl<TextBlock>("FailureText")!.Text.ShouldBe(DispatchCopy.ForPhase(DispatchPhase.Unreachable));
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A problem the relay holds, such as a cancel the service refused, is announced through the assertive failure line.</summary>
    [Fact]
    public async Task AProblemTheRelayHoldsIsAnnouncedAssertively()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var page = new DispatchPageModel(DevicesViewModel.Loaded([]), [], PairThisDevice: null, Problem: "The cancel did not reach the device.");
            var view = new DispatchPageView { DataContext = page };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            var failure = view.FindControl<TextBlock>("FailureText")!;
            failure.Text.ShouldBe("The cancel did not reach the device.");
            AutomationProperties.GetLiveSetting(failure).ShouldBe(AutomationLiveSetting.Assertive);
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A running command offers a named Cancel, and pressing it raises the command's identifier.</summary>
    [Fact]
    public async Task ARunningCommandOffersANamedCancelThatRaisesItsIdentifier()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var view = new DispatchPageView { DataContext = PageWithRunningCommand() };
            string? cancelled = null;
            view.CancelRequested = commandId => cancelled = commandId;
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            var cancel = CancelButton(view);
            AutomationProperties.GetName(cancel).ShouldBe(DispatchCopy.CancelLabel);

            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            cancelled.ShouldBe("command-1");
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>After a cancel, focus moves to the page heading, because the cancelled command's button leaves the list.</summary>
    [Fact]
    public async Task TheHeadingTakesFocusWhenACancelIsRaised()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var view = new DispatchPageView { DataContext = PageWithRunningCommand() };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            CancelButton(view).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            view.FindControl<TextBlock>("PageHeading")!.IsFocused.ShouldBeTrue();
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The pause switch and each capability switch raise the user's choice, and a page the relay rebuilds raises nothing.</summary>
    [Fact]
    public async Task ThePauseAndCapabilitySwitchesRaiseTheUsersChoice()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var pair = PairThisDeviceModel.From(new DispatchThisDevice(true, false, [new DispatchCapabilityPolicy("code", false)], []));
            var view = new DispatchPageView { DataContext = new DispatchPageModel(DevicesViewModel.Loaded([]), [], pair) };
            var pauses = new List<bool>();
            var capabilities = new List<DispatchCapabilityPolicy>();
            view.PauseChanged = pauses.Add;
            view.CapabilityChanged = capabilities.Add;
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            var pause = view.FindControl<ToggleSwitch>("PauseToggle")!;
            pause.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(pause).ShouldBe(DispatchCopy.PauseLabel);
            pause.IsChecked = true;
            pauses.ShouldBe([true]);

            var capability = view.FindControl<StackPanel>("CapabilityList")!.Children.OfType<ToggleSwitch>().Single();
            AutomationProperties.GetName(capability).ShouldBe(DispatchCopy.CapabilityLabel("code"));
            capability.IsChecked = true;
            capabilities.ShouldBe([new DispatchCapabilityPolicy("code", true)]);

            view.DataContext = new DispatchPageModel(
                DevicesViewModel.Loaded([]),
                [],
                PairThisDeviceModel.From(new DispatchThisDevice(true, true, [new DispatchCapabilityPolicy("code", true)], [])));
            pauses.ShouldBe([true]);
            capabilities.Count.ShouldBe(1);
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every control on the page that takes focus or announces a change has an accessible name.</summary>
    [Fact]
    public async Task EveryControlOnThePageThatTakesFocusOrAnnouncesHasAName()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var pair = PairThisDeviceModel.From(new DispatchThisDevice(true, false, [new DispatchCapabilityPolicy("code", false)], []));
            var page = new DispatchPageModel(
                DevicesViewModel.Loaded([new DispatchDevice("device-1", "Office Mac", Online: true, PresenceKnown: true)]),
                [DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Running, "Fix the build")],
                pair,
                "The cancel did not reach the device.");
            var view = new DispatchPageView { DataContext = page };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            ViewHarness.Unnamed(view).ShouldBeEmpty();
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The approval prompt names its three actions, and Approve once raises its decision.</summary>
    [Fact]
    public async Task TheApprovalPromptRaisesTheDecisionItNames()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var model = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], "Fix the build");
            var view = new DispatchApprovalView { DataContext = model };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            var approved = false;
            view.ApproveOnce += (_, _) => approved = true;

            view.FindControl<Button>("ApproveOnceButton")!.Content.ShouldBe(DispatchCopy.ApproveOnceLabel);
            view.FindControl<Button>("DenyButton")!.Content.ShouldBe(DispatchCopy.DenyLabel);
            view.FindControl<Button>("DenyAndPauseButton")!.Content.ShouldBe(DispatchCopy.DenyAndPauseLabel);
            AutomationProperties.GetName(view.FindControl<Button>("DenyAndPauseButton")!).ShouldBe(DispatchCopy.DenyAndPauseLabel);

            view.FindControl<Button>("ApproveOnceButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            approved.ShouldBeTrue();
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The remote task strip names its controller, and Stop raises the request for the head to act on.</summary>
    [Fact]
    public async Task TheRemoteTaskStripRaisesItsStopRequest()
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);

        await headless.RunAsync(() =>
        {
            var model = RemoteTaskStripModel.From("Laptop", "Run the tests");
            var view = new RemoteTaskStripView { DataContext = model };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            var stopRequested = false;
            view.StopRequested += (_, _) => stopRequested = true;

            var stop = view.FindControl<Button>("StopButton")!;
            stop.Content.ShouldBe(DispatchCopy.StopLabel);
            AutomationProperties.GetName(stop).ShouldBe(DispatchCopy.StopLabel);

            stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            stopRequested.ShouldBeTrue();
            window.Close();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static DispatchPageModel PageWithRunningCommand() =>
        new(DevicesViewModel.Loaded([]), [DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Running, "Fix the build")], PairThisDevice: null);

    private static Button CancelButton(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, DispatchCopy.CancelLabel));
}
