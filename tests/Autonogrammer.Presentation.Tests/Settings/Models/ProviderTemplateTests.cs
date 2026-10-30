using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>Provider templates: the list the page exposes, the form a template prefills, unique ids, the template note, and the key staying out of it.</summary>
public sealed class ProviderTemplateTests
{
    private static async Task<(ModelsSettingsPage Page, ScriptedModelsSource Source)> LoadedAsync(params ProviderInfo[] providers)
    {
        var source = new ScriptedModelsSource(providers.Length == 0 ? [ModelsTestData.Perplexity(), ModelsTestData.OpenAi()] : providers);
        source.Templates.AddRange(ModelsTestData.AllTemplates());
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source);
    }

    /// <summary>The page exposes the templates the snapshot carries, in order, and says so; a snapshot without any leaves the list empty.</summary>
    [Fact]
    public async Task PageExposesTheTemplatesOfTheSnapshot()
    {
        var (page, source) = await LoadedAsync().ConfigureAwait(true);

        page.HasTemplates.ShouldBeTrue();
        page.Templates.Select(template => template.Id).ShouldBe(["ollama-local", "example-cloud", "example-plan", "bring-your-own"]);
        page.Templates.Select(template => template.Group).ShouldBe([ProviderGroup.Local, ProviderGroup.Cloud, ProviderGroup.CodingPlan, ProviderGroup.Cloud]);

        source.Templates.Clear();
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.HasTemplates.ShouldBeFalse();
        page.Templates.ShouldBeEmpty();
    }

    /// <summary>A snapshot built with the four-argument constructor has no templates.</summary>
    [Fact]
    public void SnapshotWithoutTemplatesDefaultsToEmpty()
    {
        var snapshot = new ModelsSnapshot([ModelsTestData.OpenAi()], null, null, true);

        snapshot.Templates.ShouldBeEmpty();
    }

    /// <summary>Loading announces the template list so a view can rebind.</summary>
    [Fact]
    public async Task LoadAnnouncesTheTemplates()
    {
        var source = new ScriptedModelsSource(ModelsTestData.OpenAi());
        var page = new ModelsSettingsPage(source);
        var names = PropertyChangeRecorder.Record(page);

        source.Templates.Add(ModelsTestData.OllamaTemplate());
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        names.ShouldAnnounce(nameof(ModelsSettingsPage.Templates));
        names.ShouldAnnounce(nameof(ModelsSettingsPage.HasTemplates));
    }

    /// <summary>A local template prefills the form: loopback endpoint, no authentication, the capabilities of the template, and no model.</summary>
    [Fact]
    public async Task LocalTemplatePrefillsTheForm()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("ollama-local");

        var draft = page.Draft.ShouldNotBeNull();
        draft.IsEditing.ShouldBeFalse();
        draft.Title.ShouldBe(ModelsCopy.DraftTitleAdd);
        draft.DisplayName.ShouldBe("Ollama");
        draft.Id.ShouldBe("ollama-local");
        draft.Protocol.ShouldBe(CustomProviderProtocol.OpenAiCompatible);
        draft.EndpointText.ShouldBe("http://localhost:11434/v1");
        draft.Auth.ShouldBe(CustomProviderAuth.None);
        draft.Capabilities.ShouldBe(CustomProviderCapabilities.Chat | CustomProviderCapabilities.Embeddings);
        draft.AllowRemotePlainHttp.ShouldBeFalse();
        draft.ModelsText.ShouldBe(string.Empty);
        draft.TemplateRemark.ShouldBe("Start Ollama before testing the connection.");
        draft.IsValid.ShouldBeTrue();
        draft.HasError.ShouldBeFalse();
        draft.IsDirty.ShouldBeFalse();
        page.IsDraftOpen.ShouldBeTrue();
    }

    /// <summary>A cloud template prefills a https endpoint with a key and the Responses route.</summary>
    [Fact]
    public async Task CloudTemplatePrefillsTheForm()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("example-cloud");

        var draft = page.Draft.ShouldNotBeNull();
        draft.DisplayName.ShouldBe("Example Cloud");
        draft.Id.ShouldBe("example-cloud");
        draft.Protocol.ShouldBe(CustomProviderProtocol.OpenAiCompatible);
        draft.EndpointText.ShouldBe("https://api.example-cloud.test/v1");
        draft.Auth.ShouldBe(CustomProviderAuth.ApiKey);
        draft.SupportsChat.ShouldBeTrue();
        draft.SupportsResponses.ShouldBeTrue();
        draft.SupportsEmbeddings.ShouldBeFalse();
        draft.AllowRemotePlainHttp.ShouldBeFalse();
        draft.ModelsText.ShouldBe(string.Empty);
        draft.TemplateRemark.ShouldBeNull();
    }

    /// <summary>A coding plan template prefills the Anthropic-compatible protocol and the bearer scheme.</summary>
    [Fact]
    public async Task CodingPlanTemplatePrefillsTheForm()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("example-plan");

        var draft = page.Draft.ShouldNotBeNull();
        draft.DisplayName.ShouldBe("Example coding plan");
        draft.Protocol.ShouldBe(CustomProviderProtocol.AnthropicCompatible);
        draft.EndpointText.ShouldBe("https://plan.example-cloud.test/anthropic");
        draft.Auth.ShouldBe(CustomProviderAuth.Bearer);
        draft.Capabilities.ShouldBe(CustomProviderCapabilities.Chat);
        draft.EndpointHint.ShouldBe(ModelsCopy.EndpointHint(CustomProviderProtocol.AnthropicCompatible));
    }

    /// <summary>A template with no endpoint leaves the entry blank for the user, and a source without a date is cited without one.</summary>
    [Fact]
    public async Task TemplateWithoutEndpointLeavesTheEntryBlank()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("bring-your-own");

        var draft = page.Draft.ShouldNotBeNull();
        draft.EndpointText.ShouldBe(string.Empty);
        draft.IsValid.ShouldBeFalse();
        draft.Auth.ShouldBe(CustomProviderAuth.Bearer);
        draft.TemplateNote.ShouldBe("Started from Bring your own endpoint. Source: https://docs.example-cloud.test/byo.");
    }

    /// <summary>When another provider already has the template's id the form gets the next free numbered id; every taken number is skipped.</summary>
    [Fact]
    public async Task IdIsMadeUniqueAmongProviders()
    {
        var taken = ModelsTestData.Ollama();
        var takenTwo = taken with { Id = "ollama-local-2", DisplayName = "Ollama (second)" };
        var (page, _) = await LoadedAsync(taken, takenTwo, ModelsTestData.OpenAi()).ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("ollama-local");

        var draft = page.Draft.ShouldNotBeNull();
        draft.Id.ShouldBe("ollama-local-3");
        draft.IdError.ShouldBeNull();
        draft.IsValid.ShouldBeTrue();
        draft.IsDirty.ShouldBeFalse();

        page.Discard();
        page.BeginAddCustomFromTemplate("example-cloud");
        page.Draft.ShouldNotBeNull().Id.ShouldBe("example-cloud");
    }

    /// <summary>A template id at the id limit still gets a valid numbered id within the limit.</summary>
    [Fact]
    public async Task NumberedIdStaysWithinTheIdLimit()
    {
        var longId = new string('a', CustomProviderRules.MaxIdLength);
        var (page, source) = await LoadedAsync(ModelsTestData.OpenAi() with { Id = longId }).ConfigureAwait(true);
        source.Templates.Add(ModelsTestData.CloudTemplate() with { Id = longId });
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.BeginAddCustomFromTemplate(longId);

        var draft = page.Draft.ShouldNotBeNull();
        draft.Id.Length.ShouldBeLessThanOrEqualTo(CustomProviderRules.MaxIdLength);
        draft.Id.ShouldEndWith("-2", Case.Sensitive);
        draft.IdError.ShouldBeNull();
        draft.IsValid.ShouldBeTrue();
    }

    /// <summary>An unknown template id is a defect and throws; a blank one too. Nothing opens.</summary>
    [Fact]
    public async Task UnknownTemplateIdThrows()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        Should.Throw<ArgumentException>(() => page.BeginAddCustomFromTemplate("no-such-template"));
        Should.Throw<ArgumentException>(() => page.BeginAddCustomFromTemplate(string.Empty));
        Should.Throw<ArgumentNullException>(() => page.BeginAddCustomFromTemplate(null!));
        page.IsDraftOpen.ShouldBeFalse();
    }

    /// <summary>An open form is never replaced by a template, and a page that is not loaded opens nothing.</summary>
    [Fact]
    public async Task OpenFormAndUnloadedPageAreLeftAlone()
    {
        var unloaded = new ModelsSettingsPage(new ScriptedModelsSource());
        unloaded.BeginAddCustomFromTemplate("ollama-local");
        unloaded.IsDraftOpen.ShouldBeFalse();

        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustom();
        var blank = page.Draft.ShouldNotBeNull();
        blank.DisplayName = "Mine";

        page.BeginAddCustomFromTemplate("ollama-local");

        page.Draft.ShouldBeSameAs(blank);
        blank.DisplayName.ShouldBe("Mine");
    }

    /// <summary>The note names the template, its source page and the date it was read; a form that did not start from a template has none.</summary>
    [Fact]
    public async Task TemplateNoteNamesTheSource()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("ollama-local");
        page.Draft.ShouldNotBeNull().TemplateNote.ShouldBe(
            "Started from Ollama. Source: https://docs.ollama.com/api/openai-compatibility, read 2026-10-22.");

        page.Discard();
        page.BeginAddCustomFromTemplate("example-plan");
        page.Draft.ShouldNotBeNull().TemplateNote.ShouldBe("Started from Example coding plan.");

        page.Discard();
        page.BeginAddCustom();
        var blank = page.Draft.ShouldNotBeNull();
        blank.TemplateNote.ShouldBeNull();
        blank.TemplateRemark.ShouldBeNull();
    }

    /// <summary>The copy builds the note the same way for every combination of source and date.</summary>
    [Fact]
    public void TemplateNoteCopyHandlesEveryCombination()
    {
        var url = new Uri("https://example.test/docs");

        ModelsCopy.TemplateNote("X", url, new DateOnly(2026, 1, 2)).ShouldBe("Started from X. Source: https://example.test/docs, read 2026-01-02.");
        ModelsCopy.TemplateNote("X", url, null).ShouldBe("Started from X. Source: https://example.test/docs.");
        ModelsCopy.TemplateNote("X", null, new DateOnly(2026, 1, 2)).ShouldBe("Started from X.");
        ModelsCopy.UseTemplateName("Ollama").ShouldBe("Use the Ollama template");
        ModelsCopy.TemplateEndpointText(null).ShouldBe("You enter the endpoint.");
        ModelsCopy.TemplateEndpointText(new Uri("http://localhost:11434/v1")).ShouldBe("Endpoint: http://localhost:11434/v1");
        Should.Throw<ArgumentNullException>(() => ModelsCopy.TemplateNote(null!, null, null));
    }

    /// <summary>The empty state and the incomplete-list note tell the user to add a provider from a template or define their own.</summary>
    [Fact]
    public void EmptyAndIncompleteCopyExplainTemplates()
    {
        const string Sentence = "Add a provider from a template or define your own";

        ModelsCopy.EmptyText.ShouldContain(Sentence, Case.Sensitive);
        ModelsCopy.IncompleteNote.ShouldContain(Sentence, Case.Sensitive);
        ModelsCopy.TemplatesExplanation.ShouldContain(Sentence, Case.Sensitive);
        ModelsCopy.AddProviderLabel.ShouldBe("Add provider");
        ModelsCopy.TemplatesHeading.ShouldNotBeNullOrWhiteSpace();
        ModelsCopy.UseTemplateLabel.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A template-based form saves exactly like a custom form: one write of the prefilled values plus the typed models, no key, and the saved provider is an ordinary custom card.</summary>
    [Fact]
    public async Task SavingATemplateFormWritesACustomProviderWithoutAKey()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustomFromTemplate("ollama-local");
        var draft = page.Draft.ShouldNotBeNull();

        draft.ModelsText = "llama3\nqwen3";
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        var form = source.CustomSaved.Single();
        form.DisplayName.ShouldBe("Ollama");
        form.Id.ShouldBe("ollama-local");
        form.Protocol.ShouldBe(CustomProviderProtocol.OpenAiCompatible);
        form.BaseEndpoint.OriginalString.ShouldBe("http://localhost:11434/v1");
        form.Auth.ShouldBe(CustomProviderAuth.None);
        form.Models.ShouldBe(["llama3", "qwen3"]);
        form.Capabilities.ShouldBe(CustomProviderCapabilities.Chat | CustomProviderCapabilities.Embeddings);
        form.AllowRemotePlainHttp.ShouldBeFalse();
        source.KeysReceived.ShouldBeEmpty();
        page.Draft.ShouldBeNull();
        page.Announcement.ShouldBe(ModelsCopy.CustomSaved("Ollama"));
        var card = page.Groups.Single(group => group.Group == ProviderGroup.Custom).Cards.Single();
        card.Id.ShouldBe("ollama-local");
        card.CanEdit.ShouldBeTrue();
    }

    /// <summary>A template with plain http to a remote host is still rejected until the user allows it: a template cannot switch the safety off.</summary>
    [Fact]
    public async Task TemplateCannotAllowRemotePlainHttp()
    {
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        source.Templates.Add(ModelsTestData.CloudTemplate() with { Id = "remote-http", BaseEndpoint = new Uri("http://remote.example.test/v1") });
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.BeginAddCustomFromTemplate("remote-http");

        var draft = page.Draft.ShouldNotBeNull();
        draft.AllowRemotePlainHttp.ShouldBeFalse();
        draft.ShowAllErrors();
        draft.EndpointError.ShouldBe(ModelsCopy.ErrEndpointPlainHttpRemote);
        draft.IsValid.ShouldBeFalse();
    }

    /// <summary>No template, no template form and no template copy takes or holds a key: nothing on the surface is key-shaped.</summary>
    [Fact]
    public async Task KeyIsNeverPartOfATemplate()
    {
        var forbidden = new[] { "Key", "Secret", "Token", "Password", "Credential" };
        foreach (var type in new[] { typeof(ProviderTemplate), typeof(CustomProviderDraft) })
        {
            foreach (var property in type.GetProperties())
            {
                forbidden.ShouldAllBe(word => !property.Name.Contains(word, StringComparison.Ordinal), $"{type.Name}.{property.Name}");
            }
        }

        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        page.BeginAddCustomFromTemplate("example-cloud");

        SecretScan.ShouldNotHold(page, ModelsTestData.FakeKey);
        source.KeysReceived.ShouldBeEmpty();
    }
}
