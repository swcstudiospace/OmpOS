using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Every text the Settings window draws resolves its font through the three Greptile family tokens, and each kind of text uses the family it is meant to.</summary>
public sealed class SettingsFontTests
{
    private static readonly string[] FontTokenKeys = ["Autonogrammer.Font.Family", "Autonogrammer.Font.Display", "Autonogrammer.Font.Mono"];

    private static FontFamily Token(string key) => (FontFamily)Application.Current!.FindResource(key)!;

    private static HashSet<string> TokenNames() =>
        FontTokenKeys.Select(static key => Token(key).Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>Lists the family name of every text-bearing control that is visible under a root: text blocks, entries, buttons, checkboxes, lists and drop-downs.</summary>
    private static List<(string Control, string Text, string Family)> Fonts(Visual root)
    {
        var found = new List<(string, string, string)>();
        foreach (var control in root.GetVisualDescendants().OfType<Control>().Where(static control => control.IsEffectivelyVisible))
        {
            switch (control)
            {
                case TextBlock { Text.Length: > 0 } block:
                    found.Add((block.GetType().Name, block.Text, block.FontFamily.Name));
                    break;
                case TextBox box:
                    found.Add((box.GetType().Name, box.Text ?? string.Empty, box.FontFamily.Name));
                    break;
                case Button or CheckBox or ComboBox or ListBoxItem or Expander:
                    found.Add((control.GetType().Name, NameOf(control), ((TemplatedControl)control).FontFamily.Name));
                    break;
            }
        }

        return found;
    }

    private static string NameOf(Control control) => AutomationProperties.GetName(control) ?? string.Empty;

    private static TextBlock Block(Visual root, string text) =>
        SettingsTree.Visible<TextBlock>(root).First(block => string.Equals(block.Text, text, StringComparison.Ordinal));

    private static void EveryFontIsAToken(SettingsFixture fixture, string where)
    {
        var names = TokenNames();
        names.Count.ShouldBe(3, "the three Greptile families are three distinct tokens");
        Fonts(fixture.Window)
            .Where(font => !names.Contains(font.Family))
            .Select(static font => $"{font.Control} \"{font.Text}\" uses {font.Family}")
            .ShouldBeEmpty(where);
    }

    /// <summary>Every page, in its loading, ready and failed states, draws only the three token families.</summary>
    [Fact]
    public async Task EveryPageDrawsOnlyTheTokenFamilies()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var loading = await SettingsFixture.OpenAsync(ct, load: false).ConfigureAwait(true);
            using var ready = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            using var failed = await SettingsFixture.OpenAsync(
                ct,
                static f =>
                {
                    f.GeneralSource.LoadFailure = "failed";
                    f.AgentsSource.LoadFailure = "failed";
                    f.TabSource.LoadFailure = "failed";
                    f.PluginsSource.LoadFailure = "failed";
                    f.RulesSource.LoadFailure = "failed";
                    f.SkillsSource.LoadFailure = "failed";
                    f.CommandsSource.LoadFailure = "failed";
                    f.ModelsSource.LoadFailure = "failed";
                }).ConfigureAwait(true);

            foreach (var section in Enum.GetValues<SettingsSection>())
            {
                _ = loading.Show(section);
                EveryFontIsAToken(loading, "loading " + section);
                _ = ready.Show(section);
                EveryFontIsAToken(ready, "ready " + section);
                _ = failed.Show(section);
                EveryFontIsAToken(failed, "failed " + section);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The Models page draws only token families with a key entry, the form, the templates, a test result and the remove question showing.</summary>
    [Fact]
    public async Task TheModelsPageDrawsOnlyTheTokenFamiliesWithEveryPartOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            ProviderTemplate[] templates = [ModelsData.Template("lm-studio", "LM Studio", "http://localhost:1234/v1")];
            using var fixture = await SettingsFixture.OpenAsync(ct, f => f.ModelsSource.Snapshot = ModelsData.Snapshot() with { Templates = templates }).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            view.Templates.IsExpanded = true;
            SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "ollama").Test);
            SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "openrouter").ApiKey);
            SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "mine").Remove);
            EveryFontIsAToken(fixture, "cards with a key entry, a result and a question");

            SettingsTree.Click(fixture.Window, view.AddProvider);
            SettingsTree.Click(fixture.Window, view.SaveDraft);
            EveryFontIsAToken(fixture, "the custom provider form with its messages");

            _ = fixture.Model.RequestClose();
            EveryFontIsAToken(fixture, "the unsaved-changes prompt");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Endpoints, model selectors and ids are drawn in Space Mono, the page heading in Anybody, and prose in DM Sans.</summary>
    [Fact]
    public async Task EachKindOfTextUsesItsOwnFamily()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var mono = Token("Autonogrammer.Font.Mono");
            var display = Token("Autonogrammer.Font.Display");
            var family = Token("Autonogrammer.Font.Family");
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            SettingsTree.Visible<TextBlock>(view).First(static block => block.Classes.Contains("settings-heading")).FontFamily.ShouldBe(display);
            var ollama = ModelsPageViewTests.Card(view, "ollama");
            Block(ollama, "http://localhost:11434/v1").FontFamily.ShouldBe(mono);
            Block(ollama, "ollama/llama3").FontFamily.ShouldBe(mono);
            Block(ollama, "Ollama").FontFamily.ShouldBe(family);
            ModelsPageViewTests.Card(view, "zai-plan").PlanUrl.FontFamily.ShouldBe(mono);
            view.Search.FontFamily.ShouldBe(family);

            SettingsTree.Click(fixture.Window, view.AddProvider);
            view.Form.Endpoint.FontFamily.ShouldBe(mono);
            view.Form.Id.FontFamily.ShouldBe(mono);
            view.Form.Models.FontFamily.ShouldBe(mono);
            view.Form.DisplayName.FontFamily.ShouldBe(family);

            var plugins = fixture.Show(SettingsSection.Plugins);
            Block(plugins, "1.2.0").FontFamily.ShouldBe(mono);
        }, ct).ConfigureAwait(true);
    }
}
