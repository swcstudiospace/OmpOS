using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;
using Avalonia.Automation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Accessible names and target sizes of everything the Settings window offers or announces.</summary>
public sealed class SettingsAccessibilityTests
{
    private static readonly SettingsSection[] Sections = Enum.GetValues<SettingsSection>();

    /// <summary>Every visible focusable control and live region of every page, with the window around it, has an accessible name.</summary>
    [Fact]
    public async Task EveryPageIsNamedThroughout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            foreach (var section in Sections)
            {
                _ = fixture.Show(section);
                SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty(section.ToString());
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The pages in their failed and loading states are named too, including the Try again button.</summary>
    [Fact]
    public async Task TheLoadingAndFailedStatesAreNamed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var loading = await SettingsFixture.OpenAsync(ct, load: false).ConfigureAwait(true);
            foreach (var section in Sections)
            {
                _ = loading.Show(section);
                SettingsTree.Unnamed(loading.Window).ShouldBeEmpty("loading " + section);
            }

            using var failed = await SettingsFixture.OpenAsync(
                ct,
                static f =>
                {
                    f.GeneralSource.LoadFailure = "failed";
                    f.ModelsSource.LoadFailure = "failed";
                    f.RulesSource.LoadFailure = "failed";
                }).ConfigureAwait(true);
            foreach (var section in new[] { SettingsSection.General, SettingsSection.Models, SettingsSection.Rules })
            {
                var view = (SettingsPageViewBase)failed.Show(section);
                SettingsTree.Unnamed(failed.Window).ShouldBeEmpty("failed " + section);
                AutomationProperties.GetName(view.Status.Retry).ShouldBe(SettingsCopy.RetryLabel);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The Models page stays fully named with a key entry, the custom provider form, the open templates and the remove question showing.</summary>
    [Fact]
    public async Task TheModelsPageIsNamedWithEveryPartOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            ProviderTemplate[] templates = [ModelsData.Template("lm-studio", "LM Studio", "http://localhost:1234/v1")];
            using var fixture = await SettingsFixture.OpenAsync(ct, f => f.ModelsSource.Snapshot = ModelsData.Snapshot() with { Templates = templates }).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            view.Templates.IsExpanded = true;
            fixture.Settle();
            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty("with the templates open");

            SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "openrouter").ApiKey);
            SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "mine").Remove);
            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty("with a key entry and a remove question open");

            SettingsTree.Click(fixture.Window, view.AddProvider);
            SettingsTree.Click(fixture.Window, view.SaveDraft);
            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty("with the form open and its messages showing");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The unsaved-changes prompt and a dirty navigation entry are named, and the prompt is announced assertively.</summary>
    [Fact]
    public async Task ThePromptIsNamedAndAssertive()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            fixture.General.SetMotion(MotionPreference.Full);
            _ = fixture.Model.RequestClose();
            fixture.Settle();

            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty();
            AutomationProperties.GetLiveSetting(fixture.Window.Prompt).ShouldBe(AutomationLiveSetting.Assertive);
            AutomationProperties.GetName(fixture.Window.Prompt).ShouldBe(SettingsCopy.ClosePromptHeading);
            AutomationProperties.GetName(fixture.Window.NavigationEntry(SettingsSection.General)!).ShouldBe(SettingsCopy.NavigationEntryName(SettingsCopy.GeneralTitle, isDirty: true));
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Every button, entry, checkbox and drop-down the user can reach is at least the WCAG 2.2 minimum (24 by 24).</summary>
    [Fact]
    public async Task EveryInteractiveControlMeetsTheMinimumTargetSize()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            ProviderTemplate[] templates = [ModelsData.Template("lm-studio", "LM Studio", "http://localhost:1234/v1")];
            using var fixture = await SettingsFixture.OpenAsync(ct, f => f.ModelsSource.Snapshot = ModelsData.Snapshot() with { Templates = templates }).ConfigureAwait(true);
            var small = new List<string>();
            foreach (var section in Sections)
            {
                var page = fixture.Show(section);
                if (section == SettingsSection.Models)
                {
                    var view = (ModelsPageView)page;
                    SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "openrouter").ApiKey);
                    SettingsTree.Click(fixture.Window, view.AddProvider);
                }

                foreach (var control in SettingsTree.Visible<Control>(fixture.Window).Where(static control => control is Button or TextBox or CheckBox or ComboBox))
                {
                    if (control.TemplatedParent is null && (control.MinHeight < TargetSize.MinimumTarget || control.MinWidth < TargetSize.MinimumTarget))
                    {
                        small.Add(section + ":" + control.GetType().Name + ":" + AutomationProperties.GetName(control));
                    }
                }
            }

            small.ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }
}
