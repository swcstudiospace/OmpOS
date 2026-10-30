using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Composer;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.SlashPalette;

/// <summary>
/// The slash palette in the composer view: it opens on a leading slash and filters, keeps focus in the prompt while Up and Down move the
/// highlight, completes a name with Enter, Tab or a click without sending, closes with Escape before the normal Escape, and leaves the send
/// keys as they were (SLASH-1).
/// </summary>
public sealed class ComposerSlashPaletteViewTests
{
    private sealed class Rig : IDisposable
    {
        private readonly SlashPaletteBinding? _binding;

        internal Rig(ComposerViewModel composer, ComposerView view, Window window, SlashPaletteBinding? binding, List<string> sent, int[] stops)
        {
            Composer = composer;
            View = view;
            Window = window;
            _binding = binding;
            Sent = sent;
            Stops = stops;
        }

        internal ComposerViewModel Composer { get; }

        internal ComposerView View { get; }

        internal Window Window { get; }

        internal List<string> Sent { get; }

        internal int[] Stops { get; }

        internal TextBox Prompt => ViewHarness.Find<TextBox>(View, "PromptInput");

        internal SlashPaletteView Palette => View.SlashPaletteControl;

        public void Dispose()
        {
            _binding?.Dispose();
            Window.Close();
        }
    }

    private static Rig Open(bool withPalette = true, bool streaming = false, params OmpCommand[] commands)
    {
        var sent = new List<string>();
        var stops = new int[1];
        var composer = new ComposerViewModel(
            text =>
            {
                sent.Add(text);
                return Task.FromResult(true);
            },
            () =>
            {
                stops[0]++;
                return Task.CompletedTask;
            })
        {
            IsStreaming = streaming,
        };
        SlashPaletteBinding? binding = null;
        if (withPalette)
        {
            var control = new FakeSessionCommands();
            control.Report(commands.Length == 0 ? FakeSessionCommands.Typical : commands);
            binding = new SlashPaletteBinding(control, new SlashCommandRegistry([]), connect: null);
            composer.SlashPalette = binding.Palette;
        }

        var view = new ComposerView { DataContext = composer };
        var window = ViewHarness.Show(view, width: 800, height: 600);
        return new Rig(composer, view, window, binding, sent, stops);
    }

    private static void Type(Rig rig, string text)
    {
        rig.Prompt.Focus();
        rig.Prompt.Text = text;
        rig.Prompt.CaretIndex = text.Length;
        ViewHarness.Settle(rig.Window);
    }

    private static async Task PressAsync(Rig rig, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        rig.Prompt.Focus();
        ViewHarness.Press(rig.Window, key, physical, modifiers);

        // A send or a stop runs as tracked work: wait until the composer reports it settled, then settle the layout.
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSettled(object? sender, EventArgs e) => settled.TrySetResult();
        rig.View.WorkSettled += OnSettled;
        try
        {
            if (rig.View.IsWorking)
            {
                await settled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            rig.View.WorkSettled -= OnSettled;
        }

        ViewHarness.Settle(rig.Window);
    }

    /// <summary>A leading slash opens the palette above the prompt with every command: its name in the mono face, its input hint, its description and its source tag; the count line says how many.</summary>
    [Fact]
    public async Task ASlashOpensThePaletteWithEveryCommand()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            rig.Palette.IsVisible.ShouldBeFalse();

            Type(rig, "/");

            rig.Palette.IsVisible.ShouldBeTrue();
            rig.Palette.Rows.ItemCount.ShouldBe(4);
            rig.Palette.CountLine.Text.ShouldBe("4 commands");
            AutomationProperties.GetLiveSetting(rig.Palette.CountLine).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(rig.Palette.Rows).ShouldBe(SlashPaletteCopy.ListName);
            var texts = ViewHarness.VisibleTexts(rig.Palette);
            texts.ShouldContain("/security", StringComparer.Ordinal);
            texts.ShouldContain("<plan|scan|status>", StringComparer.Ordinal);
            texts.ShouldContain("Show current model selection", StringComparer.Ordinal);
            texts.ShouldContain(SlashPaletteCopy.SourceEngine, StringComparer.Ordinal);

            var nameBlock = rig.Palette.GetVisualDescendants().OfType<TextBlock>().First(static block => string.Equals(block.Text, "/security", StringComparison.Ordinal));
            nameBlock.FontFamily.ShouldBe(ViewHarness.MonoFont());
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The text after the slash filters the list; a slash in the middle of the text, argument text and an empty draft keep the palette closed.</summary>
    [Fact]
    public async Task TheTypedNameFiltersAndOtherDraftsKeepItClosed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();

            Type(rig, "/s");
            rig.Palette.Rows.ItemCount.ShouldBe(2);
            rig.Palette.CountLine.Text.ShouldBe("2 commands");

            Type(rig, "/sec");
            rig.Palette.Rows.ItemCount.ShouldBe(1);
            rig.Palette.CountLine.Text.ShouldBe("1 command");

            Type(rig, "/security now");
            rig.Palette.IsVisible.ShouldBeFalse();

            Type(rig, "please run /model");
            rig.Palette.IsVisible.ShouldBeFalse();

            Type(rig, "/");
            rig.Palette.IsVisible.ShouldBeTrue();
            Type(rig, string.Empty);
            rig.Palette.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A name nobody lists shows the no-match message and no list; Enter then sends the text as written.</summary>
    [Fact]
    public async Task ANameNobodyListsShowsTheMessageAndIsSentAsWritten()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();

            Type(rig, "/nonesuch");

            rig.Palette.IsVisible.ShouldBeTrue();
            rig.Palette.Rows.IsVisible.ShouldBeFalse();
            rig.Palette.Message.IsVisible.ShouldBeTrue();
            rig.Palette.Message.Text.ShouldBe("No command matches '/nonesuch'.");
            rig.Palette.CountLine.Text.ShouldBe(SlashPaletteCopy.NoCommands);

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            rig.Sent.ShouldBe(["/nonesuch"]);
            rig.Palette.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Down and Up move the highlight, wrapping, while focus and the typed text stay in the prompt.</summary>
    [Fact]
    public async Task ArrowKeysMoveTheHighlightWithoutLeavingThePrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            Type(rig, "/");
            rig.Palette.Rows.SelectedIndex.ShouldBe(0);

            await PressAsync(rig, Key.Down, PhysicalKey.ArrowDown).ConfigureAwait(true);
            rig.Palette.Rows.SelectedIndex.ShouldBe(1);
            await PressAsync(rig, Key.Down, PhysicalKey.ArrowDown).ConfigureAwait(true);
            rig.Palette.Rows.SelectedIndex.ShouldBe(2);
            await PressAsync(rig, Key.Up, PhysicalKey.ArrowUp).ConfigureAwait(true);
            await PressAsync(rig, Key.Up, PhysicalKey.ArrowUp).ConfigureAwait(true);
            await PressAsync(rig, Key.Up, PhysicalKey.ArrowUp).ConfigureAwait(true);

            rig.Palette.Rows.SelectedIndex.ShouldBe(3, "Up from the first row wraps to the last");
            rig.Prompt.IsFocused.ShouldBeTrue();
            rig.Prompt.Text.ShouldBe("/");
            rig.Composer.Draft.ShouldBe("/");
            rig.Palette.Rows.IsFocused.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter completes the highlighted command (the draft becomes the name and a space, the caret after it), closes the palette and sends nothing; the next Enter sends the completed text.</summary>
    [Fact]
    public async Task EnterCompletesTheNameAndDoesNotSend()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            Type(rig, "/se");

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            rig.Prompt.Text.ShouldBe("/security ");
            rig.Prompt.CaretIndex.ShouldBe("/security ".Length);
            rig.Composer.Draft.ShouldBe("/security ");
            rig.Palette.IsVisible.ShouldBeFalse();
            rig.Sent.ShouldBeEmpty();
            rig.Prompt.IsFocused.ShouldBeTrue();

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            rig.Sent.ShouldBe(["/security "]);
            rig.Composer.Draft.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tab completes the highlighted command the same way and does not move focus out of the prompt.</summary>
    [Fact]
    public async Task TabCompletesTheNameAndKeepsFocus()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            Type(rig, "/");
            await PressAsync(rig, Key.Down, PhysicalKey.ArrowDown).ConfigureAwait(true);

            await PressAsync(rig, Key.Tab, PhysicalKey.Tab).ConfigureAwait(true);

            rig.Prompt.Text.ShouldBe("/model ");
            rig.Prompt.IsFocused.ShouldBeTrue();
            rig.Sent.ShouldBeEmpty();
            rig.Palette.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A click on a row completes that command, sends nothing and returns focus to the prompt.</summary>
    [Fact]
    public async Task AClickOnARowCompletesThatCommand()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            Type(rig, "/");
            var row = rig.Palette.Rows.ContainerFromIndex(2).ShouldNotBeNull();
            var point = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), rig.Window).ShouldNotBeNull();

            rig.Window.MouseDown(point, MouseButton.Left);
            rig.Window.MouseUp(point, MouseButton.Left);
            ViewHarness.Settle(rig.Window);

            rig.Prompt.Text.ShouldBe("/security ");
            rig.Sent.ShouldBeEmpty();
            rig.Palette.IsVisible.ShouldBeFalse();
            rig.Prompt.IsFocused.ShouldBeTrue();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape closes the palette first and keeps the text; it does not stop a streaming response. The next Escape is the normal Escape and stops it.</summary>
    [Fact]
    public async Task EscapeClosesThePaletteBeforeTheNormalEscape()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open(streaming: true);
            Type(rig, "/mo");
            rig.Palette.IsVisible.ShouldBeTrue();

            await PressAsync(rig, Key.Escape, PhysicalKey.Escape).ConfigureAwait(true);

            rig.Palette.IsVisible.ShouldBeFalse();
            rig.Stops[0].ShouldBe(0);
            rig.Prompt.Text.ShouldBe("/mo");

            await PressAsync(rig, Key.Escape, PhysicalKey.Escape).ConfigureAwait(true);

            rig.Stops[0].ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The existing send keys are intact: plain text sends on Enter and on Ctrl+Enter, Shift+Enter adds a line, and Ctrl+Enter sends a half-typed command as written instead of accepting.</summary>
    [Fact]
    public async Task TheSendKeysAreIntact()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            Type(rig, "hello");
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);
            rig.Sent.ShouldBe(["hello"]);

            Type(rig, "again");
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control).ConfigureAwait(true);
            rig.Sent.ShouldBe(["hello", "again"]);

            Type(rig, "see /model");
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift).ConfigureAwait(true);
            rig.Sent.Count.ShouldBe(2, "Shift+Enter adds a line");

            Type(rig, "/sec");
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Control).ConfigureAwait(true);
            rig.Sent.ShouldBe(["hello", "again", "/sec"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A composer with no palette (an omp terminal) never shows one, and Enter, Tab and Escape behave as before.</summary>
    [Fact]
    public async Task WithoutAPaletteNothingOpens()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open(withPalette: false);

            Type(rig, "/sec");

            rig.Palette.IsVisible.ShouldBeFalse();
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);
            rig.Sent.ShouldBe(["/sec"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The list shows at most eight rows and scrolls past them: with twelve commands the list is exactly eight control heights tall.</summary>
    [Fact]
    public async Task TheListShowsAtMostEightRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var many = Enumerable.Range(0, 12).Select(static index => new OmpCommand($"cmd{index:D2}", $"Command {index}", null)).ToArray();
            using var rig = Open(commands: many);

            Type(rig, "/");

            rig.Palette.Rows.ItemCount.ShouldBe(12);
            var rowHeight = (double)Application.Current!.FindResource("Autonogrammer.Size.ControlHeightSm")!;
            rig.Palette.Rows.MaxHeight.ShouldBe(rowHeight * SlashPaletteModel.MaxVisibleRows);
            rig.Palette.Rows.Bounds.Height.ShouldBe(rowHeight * SlashPaletteModel.MaxVisibleRows, 1.0);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Moving past the visible rows keeps the highlighted row selected in the list (it scrolls into view), and the palette adds no tab stop and no unnamed control.</summary>
    [Fact]
    public async Task TheHighlightFollowsTheModelAndNoTabStopIsAdded()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var many = Enumerable.Range(0, 12).Select(static index => new OmpCommand($"cmd{index:D2}", $"Command {index}", null)).ToArray();
            using var rig = Open(commands: many);
            var tabOrderBefore = ViewHarness.TabOrder(rig.View).ToArray();
            var unnamedBefore = ViewHarness.Unnamed(rig.View).ToArray();
            Type(rig, "/");

            for (var step = 0; step < 10; step++)
            {
                await PressAsync(rig, Key.Down, PhysicalKey.ArrowDown).ConfigureAwait(true);
            }

            rig.Palette.Rows.SelectedIndex.ShouldBe(10);
            rig.Composer.SlashPalette.ShouldNotBeNull().SelectedIndex.ShouldBe(10);
            ViewHarness.TabOrder(rig.View).ShouldBe(tabOrderBefore);
            ViewHarness.Unnamed(rig.View).ShouldBe(unnamedBefore);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
