using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings.Ignore;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Ignore page view in a headless window: adding and removing patterns, the switches, the built-in list, the files found, naming and target sizes.</summary>
public sealed class IgnorePageViewTests
{
    private sealed record Screen(IgnoreSettingsPage Page, FakeIgnoreSource Source, IgnorePageView View, Window Window) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static async Task<Screen> ShowAsync(FakeIgnoreSource source, CancellationToken cancellationToken)
    {
        var page = new IgnoreSettingsPage(source);
        await page.LoadAsync(cancellationToken).ConfigureAwait(true);
        var view = new IgnorePageView { DataContext = page };
        var window = new Window { Width = 900, Height = 2400, Content = view };
        window.Show();
        SettingsTree.SettleSafe(window);
        return new Screen(page, source, view, window);
    }

    /// <summary>The view starts with the empty-list text, the built-in switch on, the hierarchical switch off and the built-in list hidden.</summary>
    [Fact]
    public async Task StartsWithTheDefaultsAndTheBuiltInListHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(new FakeIgnoreSource(), ct).ConfigureAwait(true);
            var texts = SettingsTree.Texts(screen.View);

            texts.ShouldContain(IgnoreCopy.Heading, StringComparer.Ordinal);
            texts.ShouldContain(IgnoreCopy.EmptyList, StringComparer.Ordinal);
            texts.ShouldContain(IgnoreCopy.NoFiles, StringComparer.Ordinal);
            screen.View.UseDefault.IsChecked.ShouldBe(true);
            screen.View.Hierarchical.IsChecked.ShouldBe(false);
            screen.View.Defaults.IsEffectivelyVisible.ShouldBeFalse();
            texts.ShouldNotContain("node_modules/", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Add (button and Enter) puts the trimmed pattern in the list as a draft, clears the box, and the row has a Remove button named with the pattern.</summary>
    [Fact]
    public async Task AddingByButtonAndByEnterDrawsRowsWithNamedRemoveButtons()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeIgnoreSource();
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);

            screen.View.Pattern.Text = "  build/ ";
            SettingsTree.Click(screen.Window, screen.View.Add);

            screen.Page.Patterns.ShouldBe(["build/"]);
            screen.View.Pattern.Text.ShouldBeNullOrEmpty();
            SettingsTree.Texts(screen.View).ShouldNotContain(IgnoreCopy.EmptyList, StringComparer.Ordinal);
            SettingsTree.Has<Button>(screen.View, IgnoreCopy.RemoveLabel + " build/").ShouldBeTrue();
            source.Saved.ShouldBeEmpty("an edit is a draft until Save");

            screen.View.Pattern.Text = "*.log";
            _ = screen.View.Pattern.Focus();
            SettingsTree.Press(screen.Window, Key.Enter, PhysicalKey.Enter);

            screen.Page.Patterns.ShouldBe(["build/", "*.log"]);
            SettingsTree.Has<Button>(screen.View, IgnoreCopy.RemoveLabel + " *.log").ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A refused pattern keeps its text in the box and shows why in the error tone; a duplicate is refused the same way.</summary>
    [Fact]
    public async Task ARefusedPatternKeepsItsTextAndSaysWhy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeIgnoreSource();
            source.Values = new IgnoreSettingsValues(["dup"], true, false);
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);

            screen.View.Pattern.Text = "dup";
            SettingsTree.Click(screen.Window, screen.View.Add);

            screen.View.Pattern.Text.ShouldBe("dup");
            screen.View.EntryError.IsEffectivelyVisible.ShouldBeTrue();
            screen.View.EntryError.Text.ShouldBe(IgnoreCopy.DuplicatePattern);
            screen.View.EntryError.Classes.ShouldContain("tone-error", StringComparer.Ordinal);
            screen.Page.Patterns.ShouldBe(["dup"]);

            screen.View.Pattern.Text = "  ";
            SettingsTree.Click(screen.Window, screen.View.Add);

            screen.View.EntryError.Text.ShouldBe(IgnoreCopy.BlankPattern);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Remove takes exactly its own pattern out of the draft and the list shows the empty text again after the last one.</summary>
    [Fact]
    public async Task RemoveTakesOutItsOwnPattern()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeIgnoreSource();
            source.Values = new IgnoreSettingsValues(["a/", "b/"], true, false);
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);

            SettingsTree.Click(screen.Window, SettingsTree.Named<Button>(screen.View, IgnoreCopy.RemoveLabel + " a/"));

            screen.Page.Patterns.ShouldBe(["b/"]);
            SettingsTree.Has<Button>(screen.View, IgnoreCopy.RemoveLabel + " a/").ShouldBeFalse();

            SettingsTree.Click(screen.Window, SettingsTree.Named<Button>(screen.View, IgnoreCopy.RemoveLabel + " b/"));

            screen.Page.Patterns.ShouldBeEmpty();
            SettingsTree.Texts(screen.View).ShouldContain(IgnoreCopy.EmptyList, StringComparer.Ordinal);
            screen.Page.IsDirty.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The two switches write to the draft, and a discard puts both the switches and the list back from the saved values.</summary>
    [Fact]
    public async Task TheSwitchesEditTheDraftAndDiscardRestoresThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(new FakeIgnoreSource(), ct).ConfigureAwait(true);

            screen.View.UseDefault.IsChecked = false;
            screen.View.Hierarchical.IsChecked = true;

            screen.Page.UseDefaultList.ShouldBeFalse();
            screen.Page.HierarchicalIgnoreFiles.ShouldBeTrue();
            screen.Page.IsDirty.ShouldBeTrue();

            screen.View.Pattern.Text = "x";
            SettingsTree.Click(screen.Window, screen.View.Add);
            screen.Page.Discard();
            SettingsTree.SettleSafe(screen.Window);

            screen.View.UseDefault.IsChecked.ShouldBe(true);
            screen.View.Hierarchical.IsChecked.ShouldBe(false);
            SettingsTree.Texts(screen.View).ShouldContain(IgnoreCopy.EmptyList, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The built-in list shows only after the button is pressed, hides again on the second press, and the button's name follows its state.</summary>
    [Fact]
    public async Task TheBuiltInListToggles()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(new FakeIgnoreSource(), ct).ConfigureAwait(true);
            AutomationProperties.GetName(screen.View.ToggleDefaults).ShouldBe(IgnoreCopy.ShowDefaultsLabel);

            SettingsTree.Click(screen.Window, screen.View.ToggleDefaults);

            screen.View.Defaults.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(screen.View).ShouldContain("node_modules/", StringComparer.Ordinal);
            AutomationProperties.GetName(screen.View.ToggleDefaults).ShouldBe(IgnoreCopy.HideDefaultsLabel);

            SettingsTree.Click(screen.Window, screen.View.ToggleDefaults);

            screen.View.Defaults.IsEffectivelyVisible.ShouldBeFalse();
            AutomationProperties.GetName(screen.View.ToggleDefaults).ShouldBe(IgnoreCopy.ShowDefaultsLabel);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The ignore files found are listed with their rule counts, and a file with a skipped rule shows what was skipped.</summary>
    [Fact]
    public async Task TheFilesFoundAreListedWithTheirProblems()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeIgnoreSource { Files = [new IgnoreFileRow(".cursorignore", 3, ["Line 2: a rule was too long"]), new IgnoreFileRow(".gitignore", 1, [])] };
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);
            var texts = SettingsTree.Texts(screen.View);

            texts.ShouldContain(".cursorignore: 3 rules, 1 skipped.", StringComparer.Ordinal);
            texts.ShouldContain(".gitignore: 1 rule.", StringComparer.Ordinal);
            texts.ShouldContain(IgnoreCopy.ProblemsLabel + ": Line 2: a rule was too long", StringComparer.Ordinal);
            texts.ShouldNotContain(IgnoreCopy.NoFiles, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Every interactive control is named and at least the minimum target size, and the only keyboard stops are the box, the buttons and the switches.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnough()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeIgnoreSource();
            source.Values = new IgnoreSettingsValues(["a/"], true, false);
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);

            SettingsTree.Unnamed(screen.View).ShouldBeEmpty();
            var controls = SettingsTree.Visible<Control>(screen.View).Where(static control => control is Button or TextBox or CheckBox && control.TemplatedParent is null).ToList();
            controls.Count.ShouldBe(6, "the box, Add, Remove, two switches and the list toggle");
            foreach (var control in controls)
            {
                control.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(control));
                control.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(control));
            }

            AutomationProperties.GetName(screen.View.Pattern).ShouldBe(IgnoreCopy.PatternLabel);
            AutomationProperties.GetName(screen.View.Add).ShouldBe(IgnoreCopy.AddLabel);
            AutomationProperties.GetName(screen.View.UseDefault).ShouldBe(IgnoreCopy.UseDefaultLabel);
            AutomationProperties.GetName(screen.View.Hierarchical).ShouldBe(IgnoreCopy.HierarchicalLabel);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A failed save shows its heading and the reason inline, in the error tone.</summary>
    [Fact]
    public async Task AFailedSaveShowsInline()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeIgnoreSource { SaveFailure = "settings file is locked" };
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);
            screen.Page.AddPattern("tmp/").ShouldBeTrue();

            (await screen.Page.SaveAsync(ct).ConfigureAwait(true)).ShouldBeFalse();
            SettingsTree.SettleSafe(screen.Window);

            var texts = SettingsTree.Texts(screen.View);
            texts.ShouldContain(IgnoreCopy.SaveFailedHeading, StringComparer.Ordinal);
            texts.ShouldContain("settings file is locked", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }
}
