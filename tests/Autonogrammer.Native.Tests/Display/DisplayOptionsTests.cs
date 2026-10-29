using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Native.Views.Blocks;
using Autonogrammer.Native.Views.Display;
using Autonogrammer.Presentation.Commands.Handlers;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Display;

/// <summary>
/// The Display switches reach the desktop views through the inherited <see cref="DisplayOptions.SettingsProperty"/>: line numbers in a
/// code block and the status glyphs of the agent rows and the thread header. Every assertion reads what the user or a screen reader gets.
/// </summary>
public sealed class DisplayOptionsTests
{
    private const string ThreeLines = "alpha\nbeta\ngamma";

    private static CodeBlockView Code(string code) => new(new CodeBlock { Id = "c-1", Language = "csharp", Code = code });

    private static TextBlock Gutter(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Single(static block => string.Equals(block.Name, "LineNumbers", StringComparison.Ordinal));

    private static TextBlock CodeTextOf(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Single(static block => string.Equals(block.Name, "CodeText", StringComparison.Ordinal));

    private static DisplaySettings Settings(FakeDisplaySettingsStore? store = null) => new(store ?? new FakeDisplaySettingsStore());

    /// <summary>With the settings set on an ancestor, a code block shows one right-aligned number per line of its code.</summary>
    [Fact]
    public async Task CodeBlockInheritsTheSettingsAndNumbersEveryLine()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var settings = Settings();
            using var host = new Host(Code(ThreeLines), settings);

            var gutter = Gutter(host.Window);
            gutter.IsEffectivelyVisible.ShouldBeTrue();
            gutter.Text.ShouldBe("1\n2\n3");
            gutter.Text!.Split('\n').Length.ShouldBe(CodeTextOf(host.Window).Text!.Split('\n').Length);
            gutter.TextAlignment.ShouldBe(Avalonia.Media.TextAlignment.Right);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Switching line numbers off hides the gutter and switching them on shows it again, while the code stays.</summary>
    [Fact]
    public async Task TogglingLineNumbersShowsAndHidesTheGutter()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var store = new FakeDisplaySettingsStore();
            var settings = Settings(store);
            using var host = new Host(Code(ThreeLines), settings);
            var gutter = Gutter(host.Window);

            await settings.SetAsync(DisplaySetting.LineNumbers, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(host.Window);
            gutter.IsEffectivelyVisible.ShouldBeFalse();
            CodeTextOf(host.Window).Text.ShouldBe(ThreeLines);
            store.Saved.Count.ShouldBe(1);

            await settings.SetAsync(DisplaySetting.LineNumbers, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(host.Window);
            gutter.IsEffectivelyVisible.ShouldBeTrue();
            gutter.Text.ShouldBe("1\n2\n3");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The gutter is decoration: no hit area, raw to assistive technology, and no unnamed control appears with it.</summary>
    [Fact]
    public async Task GutterIsHiddenFromAssistiveTechnologyAndHitTesting()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var host = new Host(Code(ThreeLines), Settings());

            var gutter = Gutter(host.Window);
            gutter.IsHitTestVisible.ShouldBeFalse();
            AutomationProperties.GetAccessibilityView(gutter).ShouldBe(AccessibilityView.Raw);
            SettingsTree.Unnamed(host.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Copy puts the code on the clipboard, without the numbers, whether the gutter shows or not.</summary>
    [Fact]
    public async Task CopyWritesOnlyTheCode()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var settings = Settings();
            using var host = new Host(Code(ThreeLines), settings);
            var copy = host.Window.GetVisualDescendants().OfType<Button>().Single();
            var clipboard = TopLevel.GetTopLevel(host.Window)?.Clipboard
                ?? throw new InvalidOperationException("The headless window has no clipboard.");

            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            (await clipboard.TryGetTextAsync().ConfigureAwait(true)).ShouldBe(ThreeLines);

            await settings.SetAsync(DisplaySetting.LineNumbers, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await clipboard.ClearAsync().ConfigureAwait(true);
            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            (await clipboard.TryGetTextAsync().ConfigureAwait(true)).ShouldBe(ThreeLines);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no settings anywhere the defaults apply (line numbers on); setting and clearing the property moves between them.</summary>
    [Fact]
    public async Task NullSettingsUseTheDefaults()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var host = new Host(Code(ThreeLines), null);
            var gutter = Gutter(host.Window);
            gutter.IsEffectivelyVisible.ShouldBeTrue();

            var settings = Settings();
            await settings.SetAsync(DisplaySetting.LineNumbers, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            DisplayOptions.SetSettings(host.Parent, settings);
            ShellHarness.Settle(host.Window);
            gutter.IsEffectivelyVisible.ShouldBeFalse();

            DisplayOptions.SetSettings(host.Parent, null);
            ShellHarness.Settle(host.Window);
            gutter.IsEffectivelyVisible.ShouldBeTrue();
            DisplayOptions.GetSettings(host.Parent).ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A block created before it is attached reads the inherited settings when it joins the tree.</summary>
    [Fact]
    public async Task BlockAttachedLaterReadsTheSettingsOnAttach()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var settings = Settings();
            await settings.SetAsync(DisplaySetting.LineNumbers, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            using var host = new Host(null, settings);
            var late = Code(ThreeLines);
            var gutter = late.FindControl<TextBlock>("LineNumbers") ?? throw new InvalidOperationException("The code block has no gutter.");
            gutter.IsVisible.ShouldBeTrue("a detached block shows the defaults until it is attached");

            host.Parent.Child = late;
            ShellHarness.Settle(host.Window);

            gutter.IsEffectivelyVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A watcher whose control left the tree no longer listens to the settings, and listens again after it returns.</summary>
    [Fact]
    public async Task WatcherStopsListeningWhileDetached()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var settings = Settings();
            var probe = new Border();
            var calls = new List<bool>();
            var watcher = new DisplayOptionsWatcher(probe, values => calls.Add(values.ShowLineNumbers));
            watcher.Values.ShowLineNumbers.ShouldBeTrue();
            using var host = new Host(probe, settings);
            calls.ShouldNotBeEmpty("attaching reads the inherited settings");
            var afterAttach = calls.Count;

            await settings.SetAsync(DisplaySetting.LineNumbers, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            calls.Count.ShouldBeGreaterThan(afterAttach);
            calls[^1].ShouldBeFalse();
            watcher.Values.ShowLineNumbers.ShouldBeFalse();

            host.Parent.Child = null;
            ShellHarness.Settle(host.Window);
            var afterDetach = calls.Count;
            await settings.SetAsync(DisplaySetting.LineNumbers, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            calls.Count.ShouldBe(afterDetach, "a detached control is not called");
            watcher.Values.ShowLineNumbers.ShouldBeTrue("the detached watcher holds no settings and reports the defaults");

            await settings.SetAsync(DisplaySetting.LineNumbers, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            host.Parent.Child = probe;
            ShellHarness.Settle(host.Window);
            calls.Count.ShouldBeGreaterThan(afterDetach);
            calls[^1].ShouldBeFalse("attaching again reads the current settings");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The gutter has one number per line: a trailing line break is not a line, and empty code still has line one.</summary>
    [Theory]
    [InlineData("x", "1")]
    [InlineData("a\nb", "1\n2")]
    [InlineData("a\nb\n", "1\n2")]
    [InlineData("a\n\nb", "1\n2\n3")]
    [InlineData("", "1")]
    public void NumberLinesCountsTheLinesOfTheCode(string code, string expected) =>
        CodeBlockView.NumberLines(code).ShouldBe(expected);

    /// <summary>Switching status indicators off hides the glyph of every agent row, keeps the status words, and switching on brings the glyphs back.</summary>
    [Fact]
    public async Task RailHidesRowGlyphsButKeepsTheStatusWords()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var fake = new FakeSessionCatalog();
            var now = clock.GetUtcNow();
            fake.Sessions.Add(PanelFixtures.Summary("run-1", SessionLifecycle.Running, now - TimeSpan.FromMinutes(5), now - TimeSpan.FromSeconds(2), "Build the rail"));
            fake.Sessions.Add(PanelFixtures.Summary("lost-1", SessionLifecycle.Lost, now - TimeSpan.FromMinutes(5), now - TimeSpan.FromSeconds(2), "Lost one"));
            var catalog = PanelFixtures.Catalog(fake, clock);
            var settings = Settings();
            using var host = new Host(new AgentRailView { DataContext = catalog }, settings);
            var icons = host.Window.GetVisualDescendants().OfType<StageIcon>().ToList();
            icons.Count.ShouldBe(catalog.Rows.Count);
            icons.ShouldAllBe(static icon => icon.IsEffectivelyVisible);

            await settings.SetAsync(DisplaySetting.StatusIndicators, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(host.Window);

            icons.ShouldAllBe(static icon => !icon.IsEffectivelyVisible);
            var texts = ViewHarness.VisibleTexts(host.Window);
            foreach (var row in catalog.Rows)
            {
                texts.ShouldContain(row.StatusText, StringComparer.Ordinal);
                texts.ShouldContain(row.Title, StringComparer.Ordinal);
            }

            await settings.SetAsync(DisplaySetting.StatusIndicators, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(host.Window);
            icons.ShouldAllBe(static icon => icon.IsEffectivelyVisible);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Switching status indicators off hides the glyph of the header's status chip; the words and the accessible name that carries the status stay.</summary>
    [Fact]
    public async Task HeaderHidesTheChipGlyphButKeepsTheNameAndWords()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var settings = Settings();
            var header = new ThreadHeaderView();
            using var host = new Host(header, settings);
            var chip = header.GetVisualDescendants().OfType<StatusChip>().Single();
            chip.Text = "Running";
            ShellHarness.Settle(host.Window);
            var name = AutomationProperties.GetName(chip);
            name.ShouldNotBeNullOrWhiteSpace();
            var glyph = chip.GetVisualDescendants().OfType<StageIcon>().Single();
            glyph.IsEffectivelyVisible.ShouldBeTrue();

            await settings.SetAsync(DisplaySetting.StatusIndicators, false, TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(host.Window);

            glyph.IsEffectivelyVisible.ShouldBeFalse();
            chip.IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(chip).ShouldBe(name);
            ViewHarness.VisibleTexts(chip).ShouldContain("Running", StringComparer.Ordinal);

            await settings.SetAsync(DisplaySetting.StatusIndicators, true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            ShellHarness.Settle(host.Window);
            glyph.IsEffectivelyVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A shown window whose content sits under a border that carries the display settings, as the main view does in the shell.</summary>
    private sealed class Host : IDisposable
    {
        public Host(Control? content, DisplaySettings? settings)
        {
            Parent = new Border { Child = content };
            DisplayOptions.SetSettings(Parent, settings);
            Window = new Window { Content = Parent, Width = 640, Height = 480 };
            Window.Show();
            ShellHarness.Settle(Window);
        }

        public Border Parent { get; }

        public Window Window { get; }

        public void Dispose() => Window.Close();
    }
}
