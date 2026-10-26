using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Native.Views.Settings;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings;

/// <summary>The API key entry of a provider card: masked, never revealed, cleared after every attempt, and never found in any control, name or tooltip afterwards.</summary>
public sealed class ModelsApiKeyTests
{
    private static async Task<(SettingsFixture Fixture, ModelsPageView View, ProviderCardView Card)> OpenEntryAsync(CancellationToken ct, Action<SettingsFixture>? configure = null)
    {
        var fixture = await SettingsFixture.OpenAsync(ct, configure).ConfigureAwait(true);
        var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
        var card = ModelsPageViewTests.Card(view, "openrouter");
        SettingsTree.Click(fixture.Window, card.ApiKey);
        return (fixture, view, ModelsPageViewTests.Card(view, "openrouter"));
    }

    private static void ShouldHoldNoKey(SettingsFixture fixture)
    {
        SettingsTree.Everything(fixture.Window).Where(static text => text.Contains(ModelsData.FakeKey, StringComparison.Ordinal)).ShouldBeEmpty("the key must not be in any text, name, help text or tooltip");
        foreach (var box in SettingsTree.Visible<TextBox>(fixture.Window))
        {
            (box.Text ?? string.Empty).ShouldNotContain(ModelsData.FakeKey);
        }
    }

    /// <summary>Add API key opens an entry whose characters are masked, with no reveal control, that takes the focus and names the provider.</summary>
    [Fact]
    public async Task AddApiKeyOpensAMaskedEntryThatCannotBeRevealed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view, card) = await OpenEntryAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                card.ApiKeyEntry.IsEffectivelyVisible.ShouldBeTrue();
                card.ApiKeyEntry.PasswordChar.ShouldBe('\u2022');
                card.ApiKeyEntry.RevealPassword.ShouldBeFalse();
                card.ApiKeyEntry.Classes.ShouldNotContain("revealPasswordButton", StringComparer.Ordinal, "the Fluent reveal button only shows with this class");
                card.ApiKeyEntry.IsFocused.ShouldBeTrue("focus moves into the entry");
                AutomationProperties.GetName(card.ApiKeyEntry).ShouldBe(ModelsCopy.ApiKeyFieldLabel("OpenRouter"));
                SettingsTree.Texts(card).ShouldContain(ModelsCopy.OmpKeyStorageNote, StringComparer.Ordinal);
                card.ApiKey.IsEffectivelyVisible.ShouldBeFalse("the entry replaces the button while it is open");
                card.SaveKey.IsEnabled.ShouldBeFalse("nothing is typed");
                AutomationProperties.GetName(card.SaveKey).ShouldBe(SettingsViewCopy.ForProvider(ModelsCopy.SaveKeyLabel, "OpenRouter"));
                SettingsTree.Visible<TextBox>(view).Count(static box => box.PasswordChar != '\0').ShouldBe(1);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Each card says where its own key lives: a provider omp knows says omp keeps it, a provider OmpOS wrote says OmpOS keeps it in its own
    /// key store and omp keeps no copy.
    /// </summary>
    [Fact]
    public async Task EachCardNamesWhereItsKeyLives()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view, openrouter) = await OpenEntryAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                var mine = ModelsPageViewTests.Card(view, "mine");
                SettingsTree.Click(fixture.Window, mine.ApiKey);
                fixture.Settle();

                var mineTexts = SettingsTree.Texts(mine);
                mineTexts.ShouldContain(ModelsCopy.OmpOSKeyStorageNote, StringComparer.Ordinal);
                mineTexts.ShouldNotContain(ModelsCopy.OmpKeyStorageNote, StringComparer.Ordinal);
                var openrouterTexts = SettingsTree.Texts(openrouter);
                openrouterTexts.ShouldContain(ModelsCopy.OmpKeyStorageNote, StringComparer.Ordinal);
                openrouterTexts.ShouldNotContain(ModelsCopy.OmpOSKeyStorageNote, StringComparer.Ordinal);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The typed key reaches the source once, exactly; the entry closes, nothing the window shows holds the key, and the card now offers Replace.</summary>
    [Fact]
    public async Task ASavedKeyIsHandedOverOnceAndLeavesNoTraceInTheWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view, card) = await OpenEntryAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                card.ApiKeyEntry.Text = ModelsData.FakeKey;
                fixture.Settle();
                card.Card!.ApiKey!.Entry.ShouldBe(ModelsData.FakeKey, "the draft is the one place the typed key lives");
                card.SaveKey.IsEnabled.ShouldBeTrue();
                SettingsTree.Descriptions(fixture.Window).Where(static text => text.Contains(ModelsData.FakeKey, StringComparison.Ordinal)).ShouldBeEmpty("no name or tooltip holds the key while it is typed");

                SettingsTree.Click(fixture.Window, card.SaveKey);

                fixture.ModelsSource.Keys.ShouldHaveSingleItem().ShouldBe(("openrouter", ModelsData.FakeKey));
                var after = ModelsPageViewTests.Card(view, "openrouter");
                after.ApiKeyEntry.IsEffectivelyVisible.ShouldBeFalse();
                after.ApiKey.Content.ShouldBe(ModelsCopy.ReplaceApiKeyLabel);
                view.Live.Text.ShouldBe(ModelsCopy.KeySaved("OpenRouter"));
                ShouldHoldNoKey(fixture);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A refused key leaves the entry open and empty with the reason beside it; a reason that quotes the key is shown with the key hidden.</summary>
    [Fact]
    public async Task ARefusedKeyClearsTheEntryAndHidesAKeyQuotedInTheReason()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, _, card) = await OpenEntryAsync(ct, static f => f.ModelsSource.KeyResult = ModelsActionResult.Failure("omp refused " + ModelsData.FakeKey)).ConfigureAwait(true);
            using (fixture)
            {
                card.ApiKeyEntry.Text = ModelsData.FakeKey;
                fixture.Settle();

                SettingsTree.Click(fixture.Window, card.SaveKey);

                card.ApiKeyEntry.IsEffectivelyVisible.ShouldBeTrue("the entry stays open so the user can try again");
                (card.ApiKeyEntry.Text ?? string.Empty).ShouldBeEmpty();
                card.Card!.ApiKey!.Entry.ShouldBeEmpty();
                card.Error.IsEffectivelyVisible.ShouldBeTrue();
                card.Error.Text.ShouldBe("omp refused [hidden]");
                AutomationProperties.GetLiveSetting(card.Error).ShouldBe(AutomationLiveSetting.Assertive);
                card.SaveKey.IsEnabled.ShouldBeFalse("the entry is empty again");
                fixture.ModelsSource.Keys.ShouldHaveSingleItem();
                ShouldHoldNoKey(fixture);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Enter in the entry saves; Esc in the entry cancels it, clears it and does not close the window; focus returns to the button.</summary>
    [Fact]
    public async Task EnterSavesAndEscCancelsWithoutClosingTheWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view, card) = await OpenEntryAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                card.ApiKeyEntry.Text = ModelsData.FakeKey;
                fixture.Settle();

                SettingsTree.Press(fixture.Window, Key.Escape, PhysicalKey.Escape);

                fixture.Model.IsClosed.ShouldBeFalse("Esc in the entry belongs to the entry");
                fixture.Model.IsClosePromptVisible.ShouldBeFalse();
                var closed = ModelsPageViewTests.Card(view, "openrouter");
                closed.ApiKeyEntry.IsEffectivelyVisible.ShouldBeFalse();
                closed.Card!.ApiKey.ShouldBeNull();
                fixture.ModelsSource.Keys.ShouldBeEmpty();
                closed.ApiKey.IsFocused.ShouldBeTrue("focus returns to the button that opened the entry");
                ShouldHoldNoKey(fixture);

                SettingsTree.Click(fixture.Window, closed.ApiKey);
                var entry = ModelsPageViewTests.Card(view, "openrouter");
                entry.ApiKeyEntry.Text = ModelsData.FakeKey;
                fixture.Settle();
                SettingsTree.Press(fixture.Window, Key.Enter, PhysicalKey.Enter);

                fixture.ModelsSource.Keys.ShouldHaveSingleItem().ShouldBe(("openrouter", ModelsData.FakeKey));
                ShouldHoldNoKey(fixture);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A key with a space is refused before it is sent; the message does not quote it and Save key stays off.</summary>
    [Fact]
    public async Task AKeyWithASpaceIsRefusedBeforeItIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, _, card) = await OpenEntryAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                card.ApiKeyEntry.Text = "bad key";
                fixture.Settle();

                card.KeyProblem.IsEffectivelyVisible.ShouldBeTrue();
                card.KeyProblem.Text.ShouldBe(ModelsCopy.ErrApiKeyCharacters);
                card.KeyProblem.Text.ShouldNotBeNull().ShouldNotContain("bad key");
                card.SaveKey.IsEnabled.ShouldBeFalse();
                fixture.ModelsSource.Keys.ShouldBeEmpty();
            }
        }, ct).ConfigureAwait(true);
    }
}
