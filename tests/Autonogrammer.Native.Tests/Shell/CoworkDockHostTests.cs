using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>Two panels show a named, keyboard-reachable strip. One panel shows none.</summary>
public sealed class CoworkDockHostTests
{
    /// <summary>Selecting a tab shows only that panel's mapped view. One panel hides the strip. F6 reaches the dock.</summary>
    [Fact]
    public async Task TheStripFollowsTheRegisteredPanels()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var catalog = new MemoryCoworkCatalog();
            using var shell = new CoworkShellModel(catalog);
            var view = new CoworkView();
            view.Attach(shell);
            var window = new Window { Content = view, Width = 1100, Height = 700 };
            try
            {
                window.Show();
                ShellHarness.Settle(window);
                shell.RegisterPanel(new Panel("one", "Artifacts", new CoworkDockContent("first panel")));
                ShellHarness.Settle(window);
                view.Dock.Tabs.IsVisible.ShouldBeFalse();
                Shown(view).ShouldBe("first panel");

                shell.RegisterPanel(new Panel("two", "Browser", new CoworkDockContent("second panel")));
                ShellHarness.Settle(window);
                view.Dock.Tabs.IsVisible.ShouldBeTrue();
                AutomationProperties.GetName(view.Dock.Tabs).ShouldBe("Dock panels");
                view.Dock.Tabs.Children.Count.ShouldBe(2);
                view.Dock.Tabs.Children.OfType<ToggleButton>().ShouldAllBe(tab => tab.IsTabStop);

                Click(view.Dock.Tabs.Children.OfType<ToggleButton>().Single(tab => string.Equals(tab.Content as string, "Browser", StringComparison.Ordinal)));
                ShellHarness.Settle(window);
                Shown(view).ShouldBe("second panel");
                view.Dock.PanelHost.Content.ShouldBeOfType<TextBlock>().Text.ShouldBe("second panel");

                view.Rail.Focus();
                window.KeyPress(Key.F6, default, PhysicalKey.F6, null);
                ShellHarness.Settle(window);
                view.Conversation.IsFocused.ShouldBeTrue();
                window.KeyPress(Key.F6, default, PhysicalKey.F6, null);
                ShellHarness.Settle(window);
                view.Dock.IsFocused.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    private static string? Shown(CoworkView view) => (view.Dock.PanelHost.Content as TextBlock)?.Text;

    private static void Click(ToggleButton button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private sealed class Panel(string id, string title, object content) : IDockPanel
    {
        public string Id => id;

        public string Title => title;

        public object Content => content;
    }
}
