using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>The custom provider form: inline errors, dirty tracking, the saved form, edit rules, and Save, Reset and Discard of the page.</summary>
public sealed class CustomProviderDraftTests
{
    private static async Task<(ModelsSettingsPage Page, ScriptedModelsSource Source)> LoadedAsync(params ProviderInfo[] providers)
    {
        var source = new ScriptedModelsSource(providers.Length == 0 ? [ModelsTestData.Ollama(), ModelsTestData.OpenAi()] : providers);
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source);
    }

    private static void FillValid(CustomProviderDraft draft)
    {
        draft.DisplayName = "  GPU box ";
        draft.Id = "gpu-box";
        draft.EndpointText = "http://localhost:8080/v1";
        draft.ModelsText = "fast\nbig";
    }

    /// <summary>A fresh form is clean, shows no error and has the documented defaults, including plain http to a remote host off.</summary>
    [Fact]
    public async Task FreshFormIsCleanWithSafeDefaults()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.BeginAddCustom();

        var draft = page.Draft.ShouldNotBeNull();
        page.IsDraftOpen.ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
        draft.IsDirty.ShouldBeFalse();
        draft.IsValid.ShouldBeFalse();
        draft.HasError.ShouldBeFalse();
        draft.AllowRemotePlainHttp.ShouldBeFalse();
        draft.Protocol.ShouldBe(CustomProviderProtocol.OpenAiCompatible);
        draft.Auth.ShouldBe(CustomProviderAuth.ApiKey);
        draft.Capabilities.ShouldBe(CustomProviderCapabilities.Chat);
        draft.SupportsChat.ShouldBeTrue();
        draft.SupportsResponses.ShouldBeFalse();
        draft.SupportsEmbeddings.ShouldBeFalse();
        draft.Title.ShouldBe(ModelsCopy.DraftTitleAdd);
    }

    /// <summary>An entry's error shows after the user changes that entry, not before; the other entries stay quiet.</summary>
    [Fact]
    public async Task ErrorsShowPerEntryAfterAnEdit()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();
        var names = PropertyChangeRecorder.Record(draft);

        draft.EndpointText = "http://example.com/v1";

        draft.EndpointError.ShouldBe(ModelsCopy.ErrEndpointPlainHttpRemote);
        draft.DisplayNameError.ShouldBeNull();
        draft.IdError.ShouldBeNull();
        names.ShouldAnnounce(nameof(CustomProviderDraft.EndpointError));
        page.HasError.ShouldBeTrue();

        draft.AllowRemotePlainHttp = true;
        draft.EndpointError.ShouldBeNull();

        draft.ShowAllErrors();
        draft.DisplayNameError.ShouldBe(ModelsCopy.ErrDisplayNameRequired);
        draft.IdError.ShouldBe(ModelsCopy.ErrIdRequired);
    }

    /// <summary>The id is checked against every provider on the page, including built-in ones.</summary>
    [Fact]
    public async Task IdIsCheckedAgainstEveryProvider()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();

        draft.Id = "openai";
        draft.IdError.ShouldBe(ModelsCopy.ErrIdTaken);
        draft.Id = "ollama-local";
        draft.IdError.ShouldBe(ModelsCopy.ErrIdTaken);
        draft.Id = "openai-2";
        draft.IdError.ShouldBeNull();
    }

    /// <summary>The capability switches and the flags agree, and the endpoint hint follows the protocol.</summary>
    [Fact]
    public async Task CapabilitiesAndHintFollowTheirChoices()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();

        draft.SupportsResponses = true;
        draft.SupportsEmbeddings = true;
        draft.SupportsChat = false;
        draft.Capabilities.ShouldBe(CustomProviderCapabilities.Responses | CustomProviderCapabilities.Embeddings);

        draft.EndpointHint.ShouldBe(ModelsCopy.EndpointHint(CustomProviderProtocol.OpenAiCompatible));
        draft.Protocol = CustomProviderProtocol.AnthropicCompatible;
        draft.EndpointHint.ShouldBe(ModelsCopy.EndpointHint(CustomProviderProtocol.AnthropicCompatible));
        draft.EndpointHint.ShouldNotBe(ModelsCopy.EndpointHint(CustomProviderProtocol.OpenAiCompatible), StringComparer.Ordinal);
    }

    /// <summary>A valid draft builds a trimmed form with the model list parsed; a broken one builds nothing.</summary>
    [Fact]
    public async Task ValidDraftBuildsTheForm()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();
        draft.TryBuildForm().ShouldBeNull();

        FillValid(draft);
        draft.Protocol = CustomProviderProtocol.AnthropicCompatible;
        draft.SupportsEmbeddings = true;

        var form = draft.TryBuildForm().ShouldNotBeNull();
        form.DisplayName.ShouldBe("GPU box");
        form.Id.ShouldBe("gpu-box");
        form.Protocol.ShouldBe(CustomProviderProtocol.AnthropicCompatible);
        form.BaseEndpoint.ShouldBe(new Uri("http://localhost:8080/v1"));
        form.Auth.ShouldBe(CustomProviderAuth.ApiKey);
        form.Models.ShouldBe(["fast", "big"]);
        form.Capabilities.ShouldBe(CustomProviderCapabilities.Chat | CustomProviderCapabilities.Embeddings);
        form.AllowRemotePlainHttp.ShouldBeFalse();
    }

    /// <summary>Typing makes the page dirty; undoing the edit makes it clean again; an invalid dirty draft is an error.</summary>
    [Fact]
    public async Task DirtyTracksTheDraftOnly()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        var names = PropertyChangeRecorder.Record(page);
        page.IsDirty.ShouldBeFalse();
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();

        draft.DisplayName = "x";
        page.IsDirty.ShouldBeTrue();
        page.HasError.ShouldBeTrue();
        names.ShouldAnnounce(nameof(ModelsSettingsPage.IsDirty));

        draft.DisplayName = string.Empty;
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();

        page.SearchText = "ollama";
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Saving a broken draft sends nothing, shows every error and returns false.</summary>
    [Fact]
    public async Task SavingABrokenDraftSendsNothing()
    {
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();
        draft.DisplayName = "GPU box";

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        source.CustomSaved.ShouldBeEmpty();
        draft.IdError.ShouldNotBeNull();
        draft.EndpointError.ShouldNotBeNull();
        page.IsDraftOpen.ShouldBeTrue();
        page.HasError.ShouldBeTrue();
    }

    /// <summary>Saving a valid draft sends the form once, closes the form, reloads the list and announces it.</summary>
    [Fact]
    public async Task SavingAValidDraftWritesAndReloads()
    {
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        FillValid(page.Draft.ShouldNotBeNull());

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.CustomSaved.Single().Id.ShouldBe("gpu-box");
        page.Draft.ShouldBeNull();
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
        source.LoadCalls.ShouldBe(2);
        var custom = page.Groups.Single(group => group.Group == ProviderGroup.Custom).Cards.Single();
        custom.Id.ShouldBe("gpu-box");
        custom.CanEdit.ShouldBeTrue();
        custom.CanRemove.ShouldBeTrue();
        page.Announcement.ShouldBe(ModelsCopy.CustomSaved("GPU box"));
    }

    /// <summary>When omp refuses the write the form stays open with the reason; the next edit clears the reason.</summary>
    [Fact]
    public async Task RefusedWriteKeepsTheFormOpenWithTheReason()
    {
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        source.CustomResult = ModelsActionResult.Failure("models.yml is not writable.");
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();
        FillValid(draft);

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        page.DraftError.ShouldBe("models.yml is not writable.");
        page.IsDraftOpen.ShouldBeTrue();
        page.HasError.ShouldBeTrue();
        source.LoadCalls.ShouldBe(1);

        draft.ModelsText = "fast";
        page.DraftError.ShouldBeNull();
        page.HasError.ShouldBeFalse();
    }

    /// <summary>Reset and Discard both close the form and clear its error; saving with no form succeeds without a write.</summary>
    [Fact]
    public async Task ResetAndDiscardClearTheDraft()
    {
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        var token = TestContext.Current.CancellationToken;
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        page.BeginAddCustom();
        FillValid(page.Draft.ShouldNotBeNull());
        page.IsDirty.ShouldBeTrue();
        page.Reset();
        page.Draft.ShouldBeNull();
        page.IsDirty.ShouldBeFalse();

        page.BeginAddCustom();
        FillValid(page.Draft.ShouldNotBeNull());
        page.Discard();
        page.Draft.ShouldBeNull();
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        source.CustomSaved.ShouldBeEmpty();
    }

    /// <summary>Editing a managed provider loads its values, locks its id, and may reuse its own id; unmanaged providers cannot be edited.</summary>
    [Fact]
    public async Task EditLoadsTheSavedValuesAndLocksTheId()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Gateway(), ModelsTestData.OpenAi()).ConfigureAwait(true);

        page.BeginEdit("openai");
        page.IsDraftOpen.ShouldBeFalse();
        page.BeginEdit("no-such-provider");
        page.IsDraftOpen.ShouldBeFalse();

        page.BeginEdit("my-gateway");
        var draft = page.Draft.ShouldNotBeNull();
        draft.IsEditing.ShouldBeTrue();
        draft.EditingId.ShouldBe("my-gateway");
        draft.Title.ShouldBe(ModelsCopy.DraftTitleEdit);
        draft.DisplayName.ShouldBe("My gateway");
        draft.EndpointText.ShouldBe("https://gateway.example.com/v1");
        draft.ModelsText.ShouldBe("fast\nbig");
        draft.IsDirty.ShouldBeFalse();
        draft.IsValid.ShouldBeTrue();

        draft.Id = "renamed";
        draft.Id.ShouldBe("my-gateway");
        draft.ModelsText = "fast\nbig\nhuge";
        page.IsDirty.ShouldBeTrue();

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        source.CustomSaved.Single().Id.ShouldBe("my-gateway");
        source.CustomSaved.Single().Models.ShouldBe(["fast", "big", "huge"]);
        page.Groups.Single(group => group.Group == ProviderGroup.Custom).Cards.Single().Models.Count.ShouldBe(3);
    }

    /// <summary>The card's Edit action opens the form for that card; a card that cannot be edited does nothing.</summary>
    [Fact]
    public async Task CardEditOpensTheForm()
    {
        var (page, _) = await LoadedAsync(ModelsTestData.Gateway(), ModelsTestData.OpenAi()).ConfigureAwait(true);
        var cards = page.Groups.SelectMany(group => group.Cards).ToDictionary(card => card.Id, StringComparer.Ordinal);

        cards["openai"].BeginEdit();
        page.IsDraftOpen.ShouldBeFalse();
        cards["my-gateway"].BeginEdit();
        page.Draft.ShouldNotBeNull().EditingId.ShouldBe("my-gateway");
    }

    /// <summary>The write notice names models.yml and the backup.</summary>
    [Fact]
    public void WriteNoticeNamesTheFileAndTheBackup()
    {
        ModelsCopy.CustomProviderWriteNotice.ShouldContain("models.yml", Case.Sensitive);
        ModelsCopy.CustomProviderWriteNotice.ShouldContain("backup", Case.Sensitive);
    }
}
