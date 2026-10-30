using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Mentions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Mentions;

/// <summary>
/// The mentions in the composer view: the picker above the prompt (focus never leaves the prompt), Enter and Tab attach instead of sending, the chips with their remove buttons,
/// the context ring beside them, and the keyboard order the other composer tests rely on.
/// </summary>
public sealed class ComposerMentionViewTests
{
    private sealed class Rig : IDisposable
    {
        internal Rig(ComposerViewModel composer, MentionComposerModel? mentions, ComposerView view, Window window, List<string> sent)
        {
            Composer = composer;
            Mentions = mentions;
            View = view;
            Window = window;
            Sent = sent;
        }

        internal ComposerViewModel Composer { get; }

        internal MentionComposerModel? Mentions { get; }

        internal ComposerView View { get; }

        internal Window Window { get; }

        internal List<string> Sent { get; }

        internal TextBox Prompt => ViewHarness.Find<TextBox>(View, "PromptInput");

        internal MentionPickerView Picker => View.MentionPickerControl;

        public void Dispose()
        {
            Mentions?.Dispose();
            Window.Close();
        }
    }

    private static Rig Open(bool withMentions = true)
    {
        var sent = new List<string>();
        var composer = new ComposerViewModel(
            text =>
            {
                sent.Add(text);
                return Task.FromResult(true);
            },
            static () => Task.CompletedTask);
        MentionComposerModel? mentions = null;
        if (withMentions)
        {
            mentions = new MentionComposerModel([new StaticMentionSource(MentionKind.File, "src/App.cs", "src/Apple.cs", "README.md"), new StaticMentionSource(MentionKind.Terminal, "build watch")]);
            composer.Mentions = mentions;
        }

        var view = new ComposerView { DataContext = composer };
        var window = ViewHarness.Show(view, width: 800, height: 600);
        return new Rig(composer, mentions, view, window, sent);
    }

    private static async Task TypeAsync(Rig rig, string text)
    {
        rig.Prompt.Focus();
        rig.Prompt.Text = text;
        rig.Prompt.CaretIndex = text.Length;
        if (rig.Mentions is not null)
        {
            await rig.Mentions.Picker.WaitSettledAsync().ConfigureAwait(true);
        }

        ViewHarness.Settle(rig.Window);
    }

    private static async Task PressAsync(Rig rig, Key key, PhysicalKey physical)
    {
        rig.Prompt.Focus();
        ViewHarness.Press(rig.Window, key, physical);
        await WorkSettledAsync(rig).ConfigureAwait(true);
    }

    private static async Task WorkSettledAsync(Rig rig)
    {
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

    /// <summary>An at sign opens the picker above the prompt: rows grouped under their titles, names in the mono face, the count in a polite live region, and no extra tab stop.</summary>
    [Fact]
    public async Task AnAtSignOpensThePickerWithGroupedRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            rig.Picker.IsVisible.ShouldBeFalse();

            await TypeAsync(rig, "@").ConfigureAwait(true);

            rig.Picker.IsVisible.ShouldBeTrue();
            rig.Picker.Rows.ItemCount.ShouldBe(4);
            rig.Picker.CountLine.Text.ShouldBe("4 items");
            AutomationProperties.GetLiveSetting(rig.Picker.CountLine).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(rig.Picker.Rows).ShouldBe(MentionCopy.ListName);
            var texts = ViewHarness.VisibleTexts(rig.Picker);
            texts.ShouldContain("Files", StringComparer.Ordinal);
            texts.ShouldContain("Terminals", StringComparer.Ordinal);
            texts.ShouldContain("src/App.cs", StringComparer.Ordinal);
            var nameBlock = rig.Picker.GetVisualDescendants().OfType<TextBlock>().First(static block => string.Equals(block.Text, "src/App.cs", StringComparison.Ordinal));
            nameBlock.FontFamily.ShouldBe(ViewHarness.MonoFont());
            ViewHarness.TabStops(rig.Picker).ShouldBeEmpty();
            rig.Prompt.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The text after the at sign filters the list, a name nobody has shows the message and no list, and a space after the token closes the picker.</summary>
    [Fact]
    public async Task TypingFiltersAndASpaceCloses()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();

            await TypeAsync(rig, "see @Apple").ConfigureAwait(true);
            rig.Picker.Rows.ItemCount.ShouldBe(1);
            rig.Picker.CountLine.Text.ShouldBe("1 item");

            await TypeAsync(rig, "see @zzzz").ConfigureAwait(true);
            rig.Picker.Rows.IsVisible.ShouldBeFalse();
            rig.Picker.Message.Text.ShouldBe(MentionCopy.NothingMatches);
            rig.Picker.Message.IsVisible.ShouldBeTrue();

            await TypeAsync(rig, "see @Apple now").ConfigureAwait(true);
            rig.Picker.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Down and Up move the highlight, wrapping, while focus and the text stay in the prompt.</summary>
    [Fact]
    public async Task ArrowKeysMoveTheHighlightWithoutLeavingThePrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            await TypeAsync(rig, "@").ConfigureAwait(true);
            rig.Picker.Rows.SelectedIndex.ShouldBe(0);

            await PressAsync(rig, Key.Down, PhysicalKey.ArrowDown).ConfigureAwait(true);
            rig.Picker.Rows.SelectedIndex.ShouldBe(1);
            await PressAsync(rig, Key.Up, PhysicalKey.ArrowUp).ConfigureAwait(true);
            await PressAsync(rig, Key.Up, PhysicalKey.ArrowUp).ConfigureAwait(true);

            rig.Picker.Rows.SelectedIndex.ShouldBe(3, "Up from the first row wraps to the last");
            rig.Prompt.IsFocused.ShouldBeTrue();
            rig.Prompt.Text.ShouldBe("@");
            rig.Sent.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter attaches the highlighted item: the token becomes the mention text with the caret after it, a chip appears, the picker closes and nothing is sent; the next Enter sends.</summary>
    [Fact]
    public async Task EnterAttachesAndDoesNotSend()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            await TypeAsync(rig, "see @Apple").ConfigureAwait(true);

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            rig.Prompt.Text.ShouldBe("see @src/Apple.cs ");
            rig.Prompt.CaretIndex.ShouldBe("see @src/Apple.cs ".Length);
            rig.Composer.Draft.ShouldBe("see @src/Apple.cs ");
            rig.Picker.IsVisible.ShouldBeFalse();
            rig.Sent.ShouldBeEmpty();
            rig.Prompt.IsFocused.ShouldBeTrue();
            rig.View.ContextRowControl.IsVisible.ShouldBeTrue();
            rig.View.MentionChipsList.ItemCount.ShouldBe(1);
            ViewHarness.VisibleTexts(rig.View.MentionChipsList).ShouldContain("src/Apple.cs", StringComparer.Ordinal);

            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            rig.Sent.ShouldBe(["see @src/Apple.cs "]);
            rig.View.MentionChipsList.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A click on a row attaches it and returns the focus to the prompt.</summary>
    [Fact]
    public async Task AClickOnARowAttachesIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            await TypeAsync(rig, "@read").ConfigureAwait(true);
            rig.Picker.Rows.ItemCount.ShouldBe(1);
            var row = rig.Picker.Rows.ContainerFromIndex(0).ShouldNotBeNull();
            var point = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), rig.Window).ShouldNotBeNull();

            rig.Window.MouseDown(point, MouseButton.Left);
            rig.Window.MouseUp(point, MouseButton.Left);
            await WorkSettledAsync(rig).ConfigureAwait(true);

            rig.Composer.Draft.ShouldBe("@README.md ");
            rig.Prompt.Text.ShouldBe("@README.md ");
            rig.View.MentionChipsList.ItemCount.ShouldBe(1);
            rig.Picker.IsVisible.ShouldBeFalse();
            rig.Prompt.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape closes the picker and keeps the text.</summary>
    [Fact]
    public async Task EscapeClosesThePickerAndKeepsTheText()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            await TypeAsync(rig, "@App").ConfigureAwait(true);
            rig.Picker.IsVisible.ShouldBeTrue();

            await PressAsync(rig, Key.Escape, PhysicalKey.Escape).ConfigureAwait(true);

            rig.Picker.IsVisible.ShouldBeFalse();
            rig.Prompt.Text.ShouldBe("@App");
            rig.Composer.Draft.ShouldBe("@App");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A chip names the item with its size and has a remove button of the target size, named for the item; removing it removes the text and the chip.</summary>
    [Fact]
    public async Task ARemoveButtonRemovesTheChipAndItsText()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            await TypeAsync(rig, "see @Apple").ConfigureAwait(true);
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);
            var remove = rig.View.MentionChipsList.GetVisualDescendants().OfType<Button>().Single();

            AutomationProperties.GetName(remove).ShouldBe("Remove src/Apple.cs");
            remove.MinHeight.ShouldBeGreaterThanOrEqualTo(32);
            remove.MinWidth.ShouldBeGreaterThanOrEqualTo(32);
            ViewHarness.VisibleTexts(rig.View.MentionChipsList).ShouldContain("10 B", StringComparer.Ordinal);
            ViewHarness.TabOrder(rig.View)[^1].ShouldBe("Remove src/Apple.cs");

            ViewHarness.Click(rig.Window, remove);

            rig.Composer.Draft.ShouldBe("see ");
            rig.Prompt.Text.ShouldBe("see ");
            rig.View.MentionChipsList.IsVisible.ShouldBeFalse();
            rig.View.ContextRowControl.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The ring beside the chips shows the usage as a percentage and, from 70 percent on, the state in words; the arc follows the percentage.</summary>
    [Fact]
    public async Task TheRingShowsUsageAndTheStateInWords()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            var ring = new ContextRingModel();
            rig.Composer.ContextRing = ring;
            ViewHarness.Settle(rig.Window);
            rig.View.ContextRingControl.IsVisible.ShouldBeFalse();
            rig.View.ContextRowControl.IsVisible.ShouldBeFalse();

            ring.ReportUsage(300, 1000);
            ViewHarness.Settle(rig.Window);
            rig.View.ContextRowControl.IsVisible.ShouldBeTrue();
            rig.View.ContextRingControl.Percent.Text.ShouldBe("30%");
            rig.View.ContextRingControl.State.IsVisible.ShouldBeFalse();
            rig.View.ContextRingControl.Arc.SweepAngle.ShouldBe(108, 0.01);

            ring.ReportUsage(750, 1000);
            ViewHarness.Settle(rig.Window);
            rig.View.ContextRingControl.State.Text.ShouldBe(ContextRingCopy.StateWarning);
            rig.View.ContextRingControl.Arc.Classes.Contains("ring-warning").ShouldBeTrue();

            ring.ReportUsage(950, 1000);
            ViewHarness.Settle(rig.Window);
            rig.View.ContextRingControl.State.Text.ShouldBe(ContextRingCopy.StateCritical);
            rig.View.ContextRingControl.Arc.Classes.Contains("ring-critical").ShouldBeTrue();
            rig.View.ContextRingControl.Arc.Classes.Contains("ring-warning").ShouldBeFalse();
            AutomationProperties.GetName(rig.View.ContextRingControl).ShouldBe(ring.AutomationName);
            ToolTip.GetTip(rig.View.ContextRingControl.GetVisualDescendants().OfType<StackPanel>().First()).ShouldBe(ring.Tooltip);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A composer with no mention model never opens a picker, and its authored controls all have accessible names.</summary>
    [Fact]
    public async Task WithoutMentionsNothingOpensAndEveryControlIsNamed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open(withMentions: false);

            await TypeAsync(rig, "@App").ConfigureAwait(true);

            rig.Picker.IsVisible.ShouldBeFalse();
            ViewHarness.Unnamed(rig.View).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With a chip showing, every authored control still has a name and the prompt is still the first tab stop.</summary>
    [Fact]
    public async Task WithChipsEveryControlIsNamedAndThePromptStaysFirst()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            await TypeAsync(rig, "@Apple").ConfigureAwait(true);
            await PressAsync(rig, Key.Enter, PhysicalKey.Enter).ConfigureAwait(true);

            ViewHarness.Unnamed(rig.View).ShouldBeEmpty();
            ViewHarness.TabOrder(rig.View)[0].ShouldBe("Message");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
