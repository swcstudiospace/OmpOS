using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Presentation.Composer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.ModelPicker;

/// <summary>The model chip of the composer and its flyout, driven the way a user drives them: keyboard, pointer and the accessibility tree.</summary>
public sealed class ModelPickerViewTests
{
    private const string SonnetName = "Model: anthropic / Claude Sonnet";

    private static Avalonia.Media.FontFamily Token(string key) => (Avalonia.Media.FontFamily)Application.Current!.FindResource(key)!;

    private static object? Focused(ModelPickerFixture fixture) => fixture.Window.FocusManager!.GetFocusedElement();

    /// <summary>A composer whose session offers no model control shows no chip and adds no tab stop.</summary>
    [Fact]
    public async Task WithoutAPickerTheChipIsHidden()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open(withPicker: false);

            fixture.Chip.IsVisible.ShouldBeFalse();
            ViewHarness.TabOrder(fixture.View).ShouldNotContain(static name => name.StartsWith("Model", StringComparison.Ordinal));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The chip names the current model, is a named target of at least the WCAG minimum (24 px) in the footer's tab order after the buttons, and draws its text in the mono token.</summary>
    [Fact]
    public async Task TheChipShowsTheModelAndIsAccessible()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();

            fixture.Chip.IsVisible.ShouldBeTrue();
            fixture.Chip.ChipLabel.Text.ShouldBe("anthropic / Claude Sonnet");
            AutomationProperties.GetName(fixture.Chip.Chip).ShouldBe(SonnetName);
            AutomationProperties.GetHelpText(fixture.Chip.Chip).ShouldNotBeNullOrWhiteSpace();
            fixture.Chip.Chip.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            fixture.Chip.Chip.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            fixture.Chip.ChipLabel.FontFamily.ShouldBe(Token("Autonogrammer.Font.Mono"));

            var order = ViewHarness.TabOrder(fixture.View);
            order.ShouldContain("Message", StringComparer.Ordinal);
            order[^1].ShouldBe(SonnetName);
            ViewHarness.Unnamed(fixture.View).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening the flyout puts focus in the search box, groups the models under provider headings, and names the list and the status line.</summary>
    [Fact]
    public async Task OpeningTheFlyoutFocusesTheSearchAndListsTheGroups()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();

            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();

            fixture.Chip.IsOpen.ShouldBeTrue();
            Focused(fixture).ShouldBe(fixture.Chip.Search);
            AutomationProperties.GetName(fixture.Chip.Search).ShouldBe("Search models");
            AutomationProperties.GetName(fixture.Chip.Models).ShouldBe("Models");
            AutomationProperties.GetLiveSetting(fixture.Chip.Status).ShouldBe(AutomationLiveSetting.Polite);
            fixture.Chip.Models.ItemCount.ShouldBe(5);
            fixture.Chip.Picker!.Rows.Select(static row => row.AutomationName).ShouldBe(
                ["Provider anthropic", "Claude Sonnet, anthropic, current model", "Claude Opus, anthropic", "Provider openai", "GPT X, openai"]);
            fixture.Chip.Status.Text.ShouldBe("anthropic/claude-sonnet");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A provider heading takes no pointer input and no focus, so it can never be selected.</summary>
    [Fact]
    public async Task ProviderHeadingsAreNotSelectable()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();

            var items = fixture.Chip.Models.GetVisualDescendants().OfType<ListBoxItem>().ToList();

            items.Count.ShouldBe(5);
            foreach (var item in items)
            {
                var isHeading = item.DataContext is ModelPickerRow { IsHeading: true };
                item.IsHitTestVisible.ShouldBe(!isHeading);
                item.Focusable.ShouldBeFalse();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Down moves the highlighted model, Enter switches to it, the chip shows the new model and focus returns to the prompt.</summary>
    [Fact]
    public async Task ArrowKeysAndEnterSwitchTheModelAndReturnFocusToThePrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();

            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);
            var highlighted = fixture.Chip.Picker!.HighlightedRow.ShouldNotBeNull();
            highlighted.Choice!.Value.ShouldBe("anthropic/claude-opus");
            fixture.Chip.Models.SelectedItem.ShouldBe(highlighted);

            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);
            ViewHarness.Press(fixture.Window, Key.Up, PhysicalKey.ArrowUp);
            fixture.Chip.Picker.HighlightedRow!.Choice!.Value.ShouldBe("anthropic/claude-opus");

            ViewHarness.Press(fixture.Window, Key.Enter, PhysicalKey.Enter);

            fixture.Control.Requests.ShouldBe(["anthropic/claude-opus"]);
            fixture.Chip.ChipLabel.Text.ShouldBe("anthropic / Claude Opus");
            AutomationProperties.GetName(fixture.Chip.Chip).ShouldBe("Model: anthropic / Claude Opus");
            fixture.Chip.IsOpen.ShouldBeFalse();
            Focused(fixture).ShouldBe(ViewHarness.Find<TextBox>(fixture.View, "PromptInput"));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape closes the flyout without switching and returns focus to the prompt.</summary>
    [Fact]
    public async Task EscapeClosesTheFlyoutAndReturnsFocusToThePrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();
            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);

            ViewHarness.Press(fixture.Window, Key.Escape, PhysicalKey.Escape);

            fixture.Chip.IsOpen.ShouldBeFalse();
            fixture.Control.Requests.ShouldBeEmpty();
            fixture.Chip.ChipLabel.Text.ShouldBe("anthropic / Claude Sonnet");
            Focused(fixture).ShouldBe(ViewHarness.Find<TextBox>(fixture.View, "PromptInput"));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Typing in the search box filters the list; a filter that matches nothing says so in the status line.</summary>
    [Fact]
    public async Task TypingFiltersTheList()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();

            fixture.Chip.Search.Text = "gpt";
            fixture.Settle();

            fixture.Chip.Picker!.FilterText.ShouldBe("gpt");
            fixture.Chip.Models.ItemCount.ShouldBe(2);
            fixture.Chip.Picker.HighlightedRow!.Choice!.Value.ShouldBe("openai/gpt-x");

            fixture.Chip.Search.Text = "zzz";
            fixture.Settle();

            fixture.Chip.Models.ItemCount.ShouldBe(0);
            fixture.Chip.Status.Text.ShouldBe("No models match.");

            // Closing and opening again starts from the full list.
            fixture.Chip.Close();
            fixture.Settle();
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();
            fixture.Chip.Search.Text.ShouldBeNullOrEmpty();
            fixture.Chip.Models.ItemCount.ShouldBe(5);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A click on a model switches to it.</summary>
    [Fact]
    public async Task ClickingAModelSwitchesToIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();
            var item = fixture.Chip.Models.GetVisualDescendants().OfType<ListBoxItem>()
                .Single(static candidate => candidate.DataContext is ModelPickerRow { Choice.Value: "openai/gpt-x" });
            var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), fixture.Window)!.Value;

            fixture.Window.MouseDown(center, MouseButton.Left);
            fixture.Window.MouseUp(center, MouseButton.Left);
            fixture.Settle();

            fixture.Control.Requests.ShouldBe(["openai/gpt-x"]);
            fixture.Chip.ChipLabel.Text.ShouldBe("openai / GPT X");
            fixture.Chip.IsOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A switch omp refuses keeps the flyout open, keeps the model, and says why in the polite status line in the error tone.</summary>
    [Fact]
    public async Task ARefusedSwitchStaysOpenWithTheReasonInTheLiveRegion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open(answer: _ => new OmpModelSwitchResult(OmpModelSwitchOutcome.Failed, "anthropic/claude-sonnet", "omp could not switch the model: no key."));
            fixture.Chip.Open().ShouldBeTrue();
            fixture.Settle();
            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);

            ViewHarness.Press(fixture.Window, Key.Enter, PhysicalKey.Enter);

            fixture.Chip.IsOpen.ShouldBeTrue();
            fixture.Chip.ChipLabel.Text.ShouldBe("anthropic / Claude Sonnet");
            fixture.Chip.Status.Text.ShouldBe("omp could not switch the model: no key.");
            fixture.Chip.Status.Classes.Contains("error").ShouldBeTrue();
            fixture.Chip.Status.Classes.Contains("muted").ShouldBeFalse();
            AutomationProperties.GetLiveSetting(fixture.Chip.Status).ShouldBe(AutomationLiveSetting.Polite);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While a response streams the chip is disabled with its reason, the flyout cannot be opened, and it is enabled again afterwards.</summary>
    [Fact]
    public async Task AnActiveRunDisablesTheChip()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();

            fixture.Composer.IsStreaming = true;
            fixture.Settle();

            fixture.Chip.Chip.IsEnabled.ShouldBeFalse();
            fixture.Chip.Open().ShouldBeFalse();
            AutomationProperties.GetHelpText(fixture.Chip.Chip).ShouldBe("The model cannot be changed while a response is running.");
            ViewHarness.TabOrder(fixture.View).ShouldNotContain(SonnetName, StringComparer.Ordinal);

            fixture.Composer.IsStreaming = false;
            fixture.Settle();

            fixture.Chip.Chip.IsEnabled.ShouldBeTrue();
            fixture.Chip.Open().ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A model change the engine reports on its own, even from another thread, moves the chip label.</summary>
    [Fact]
    public async Task AnExternalModelChangeMovesTheChip()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = ModelPickerFixture.Open();

            await Task.Run(() => fixture.Control.Report(ModelPickerFixture.Models, "openai/gpt-x"), TestContext.Current.CancellationToken).ConfigureAwait(true);
            fixture.Settle();

            fixture.Chip.ChipLabel.Text.ShouldBe("openai / GPT X");
            AutomationProperties.GetName(fixture.Chip.Chip).ShouldBe("Model: openai / GPT X");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An engine that offers no model option hides the chip; a later report of models shows it again.</summary>
    [Fact]
    public async Task NoModelOptionHidesTheChipUntilModelsAreReported()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = ModelPickerFixture.Open();

            fixture.Control.Report([], null);
            fixture.Settle();
            fixture.Chip.IsVisible.ShouldBeFalse();

            fixture.Control.Report(ModelPickerFixture.Models, "anthropic/claude-opus");
            fixture.Settle();
            fixture.Chip.IsVisible.ShouldBeTrue();
            fixture.Chip.ChipLabel.Text.ShouldBe("anthropic / Claude Opus");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
