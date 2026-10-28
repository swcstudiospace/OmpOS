using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Modes;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Modes;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Modes;

/// <summary>
/// The custom-mode chip of the composer footer on the headless desktop: hidden until a skill is pinned, then the name and the colour of the skill with a button
/// that exits the mode; the list of skills pins one from the keyboard, stays open with the reason when a skill cannot be pinned, and every control is named.
/// </summary>
public sealed class CustomModeChipViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ModeSkill Skill(string name, string description = "", string? color = null) =>
        new(name, description, Icon: null, color, SlashScope.Project, ".autonogrammer/skills/" + name + "/SKILL.md");

    private static FakeModeSkillSource SourceWith(params ModeSkill[] skills)
    {
        var source = new FakeModeSkillSource { Skills = [.. skills] };
        foreach (var skill in skills)
        {
            source.Bodies[skill.Name] = "Instructions of " + skill.Name;
        }

        return source;
    }

    private static string[] ToneOf(CustomModeChipView view) => [.. view.Dot.Classes.Where(static name => name.StartsWith("mode-", StringComparison.Ordinal))];

    private static void PressEnterInList(CustomModeChipView view) =>
        view.Skills.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, PhysicalKey = PhysicalKey.Enter, Source = view.Skills });

    /// <summary>The chip is hidden while no mode is pinned; pinning shows the mode name, its colour and the status; the clear button exits the mode and the chip hides again.</summary>
    [Fact]
    public async Task TheChipShowsTheModeOnceAndTheClearButtonExitsIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new CustomModeModel(SourceWith(Skill("review", "Review the diff", "green")));
            await model.RefreshAsync(Token).ConfigureAwait(true);
            var view = new CustomModeChipView { DataContext = model };
            var window = ViewHost.Show(view);
            view.Chip.IsVisible.ShouldBeFalse();
            view.Picker.IsVisible.ShouldBeTrue();

            (await model.UseAsModeAsync("review", Token).ConfigureAwait(true)).ShouldBeTrue();
            ShellHarness.Settle(window);
            view.Chip.IsVisible.ShouldBeTrue();
            view.ChipLabel.Text.ShouldBe(CustomModeCopy.ChipText("review"));
            ToneOf(view).ShouldBe(["mode-success"]);
            AutomationProperties.GetName(view.ClearButton).ShouldBe(CustomModeCopy.ChipName("review"));
            view.Status.Text.ShouldBe(CustomModeCopy.Started("review"));

            SettingsTree.Click(window, view.ClearButton);
            model.IsActive.ShouldBeFalse();
            view.Chip.IsVisible.ShouldBeFalse();
            view.Status.Text.ShouldBe(CustomModeCopy.Exited);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The dot takes its tone from the colour key of the skill, through the status tokens: one tone class at a time, and a skill without a known colour is the default.</summary>
    [Fact]
    public async Task TheDotTakesOneToneFromTheColourKeyOfTheSkill()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var keys = new (string? Color, string Tone)[]
            {
                ("green", "mode-success"),
                ("yellow", "mode-warning"),
                ("orange", "mode-warning"),
                ("red", "mode-error"),
                ("purple", "mode-accent"),
                ("blue", "mode-accent"),
                ("brand", "mode-accent"),
                (null, "mode-default"),
                ("not-a-colour", "mode-default"),
            };
            var skills = keys.Select(static (key, index) => Skill("skill" + index, color: key.Color)).ToArray();
            var model = new CustomModeModel(SourceWith(skills));
            await model.RefreshAsync(Token).ConfigureAwait(true);
            var view = new CustomModeChipView { DataContext = model };
            var window = ViewHost.Show(view);

            for (var index = 0; index < keys.Length; index++)
            {
                (await model.UseAsModeAsync(skills[index].Name, Token).ConfigureAwait(true)).ShouldBeTrue();
                ShellHarness.Settle(window);
                ToneOf(view).ShouldBe([keys[index].Tone], "colour " + (keys[index].Color ?? "none"));
            }

            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Opening the list reads the skills again, lists them with their names, and Enter on the highlighted skill pins it, closes the list and shows the chip.</summary>
    [Fact]
    public async Task OpeningTheListReadsTheSkillsAndEnterPinsTheHighlightedOne()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = SourceWith(Skill("review", "Review the diff"), Skill("plan", "Plan the work"));
            var model = new CustomModeModel(source);
            var view = new CustomModeChipView { DataContext = model };
            var window = ViewHost.Show(view);
            source.ListCalls.ShouldBe(0);

            view.Open();
            ShellHarness.Settle(window);
            view.IsOpen.ShouldBeTrue();
            source.ListCalls.ShouldBe(1);
            view.Skills.ItemCount.ShouldBe(2);
            view.EmptyLine.IsVisible.ShouldBeFalse();
            view.Skills.SelectedItem.ShouldBeSameAs(model.Skills[0]);

            view.Skills.SelectedItem = model.Skills[1];
            PressEnterInList(view);
            ShellHarness.Settle(window);

            model.Active.ShouldNotBeNull().Skill.Name.ShouldBe("plan");
            view.IsOpen.ShouldBeFalse();
            view.Chip.IsVisible.ShouldBeTrue();
            view.ChipLabel.Text.ShouldBe(CustomModeCopy.ChipText("plan"));
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A skill that cannot be pinned leaves the list open, keeps the mode that was on, and says why in the status line as an error.</summary>
    [Fact]
    public async Task ASkillThatCannotBePinnedKeepsTheListOpenWithTheReason()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = SourceWith(Skill("review"), Skill("plan"));
            _ = source.Bodies.Remove("plan");
            var model = new CustomModeModel(source);
            var view = new CustomModeChipView { DataContext = model };
            var window = ViewHost.Show(view);
            view.Open();
            ShellHarness.Settle(window);

            (await model.UseAsModeAsync("review", Token).ConfigureAwait(true)).ShouldBeTrue();
            view.Skills.SelectedItem = model.Skills[1];
            PressEnterInList(view);
            ShellHarness.Settle(window);

            model.Active.ShouldNotBeNull().Skill.Name.ShouldBe("review");
            view.IsOpen.ShouldBeTrue();
            view.Status.Text.ShouldBe(CustomModeCopy.Unreadable("plan"));
            view.Status.Classes.Contains("tone-error").ShouldBeTrue();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With no skill at all the list says so in the model's words, and Enter in the empty list pins nothing.</summary>
    [Fact]
    public async Task AnEmptyListSaysWhyAndPinsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = new CustomModeModel(new FakeModeSkillSource());
            var view = new CustomModeChipView { DataContext = model };
            var window = ViewHost.Show(view);

            view.Open();
            ShellHarness.Settle(window);
            view.Skills.ItemCount.ShouldBe(0);
            view.EmptyLine.IsVisible.ShouldBeTrue();
            view.EmptyLine.Text.ShouldBe(CustomModeCopy.Empty);

            PressEnterInList(view);
            model.IsActive.ShouldBeFalse();
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every keyboard stop is named, in the order Tab reaches them; every button is at least the minimum target; the status line is a polite live region.</summary>
    [Fact]
    public async Task EveryControlIsNamedInTabOrderAndBigEnoughAndTheStatusIsLive()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new CustomModeModel(SourceWith(Skill("review")));
            await model.RefreshAsync(Token).ConfigureAwait(true);
            _ = await model.UseAsModeAsync("review", Token).ConfigureAwait(true);
            var view = new CustomModeChipView { DataContext = model };
            var window = ViewHost.Show(view);

            SettingsTree.Unnamed(view).ShouldBeEmpty();
            SettingsTree.TabStops(view).Select(static stop => AutomationProperties.GetName(stop))
                .ShouldBe([CustomModeCopy.ChipName("review"), CustomModeCopy.UseAsModeLabel]);
            foreach (var button in SettingsTree.Visible<Button>(view).Where(static button => button.TemplatedParent is null))
            {
                button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            AutomationProperties.GetLiveSetting(view.Status).ShouldBe(AutomationLiveSetting.Polite);
            window.Close();
        }, Token).ConfigureAwait(true);
    }
}
