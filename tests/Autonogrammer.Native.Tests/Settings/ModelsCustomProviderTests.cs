using Avalonia.Automation;
using Avalonia.Controls;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The custom provider form of the Models page, its templates, and the Edit and Remove actions of a provider Autonogrammer wrote.</summary>
public sealed class ModelsCustomProviderTests
{
    private static async Task<(SettingsFixture Fixture, ModelsPageView View)> OpenAsync(CancellationToken ct, Action<SettingsFixture>? configure = null)
    {
        var fixture = await SettingsFixture.OpenAsync(ct, configure).ConfigureAwait(true);
        return (fixture, (ModelsPageView)fixture.Show(SettingsSection.Models));
    }

    private static void OpenForm(SettingsFixture fixture, ModelsPageView view)
    {
        SettingsTree.Click(fixture.Window, view.AddProvider);
        fixture.Models.IsDraftOpen.ShouldBeTrue();
    }

    private static IReadOnlyList<string> Messages(ModelsPageView view) =>
        [.. view.Form.ErrorLines.Where(static line => line.IsEffectivelyVisible).Select(static line => line.Text!)];

    private static void Choose<T>(ComboBox box, T value)
        where T : struct, Enum =>
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().First(item => item.Tag is T tag && EqualityComparer<T>.Default.Equals(tag, value));

    /// <summary>Add provider opens a blank form with focus in its first field, no message showing, and the notices that matter before saving.</summary>
    [Fact]
    public async Task AddProviderOpensABlankFormWithItsNotices()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                OpenForm(fixture, view);
                var form = view.Form;
                var texts = SettingsTree.Texts(form);

                form.IsEffectivelyVisible.ShouldBeTrue();
                texts.ShouldContain(ModelsCopy.DraftTitleAdd, StringComparer.Ordinal);
                texts.ShouldContain(ModelsCopy.CustomProviderWriteNotice, StringComparer.Ordinal);
                texts.ShouldContain(ModelsCopy.AllowPlainHttpHint, StringComparer.Ordinal);
                texts.ShouldContain(ModelsCopy.CustomKeyHint, StringComparer.Ordinal);
                form.DisplayName.IsFocused.ShouldBeTrue();
                Messages(view).ShouldBeEmpty("a blank form does not open covered in errors");
                form.Chat.IsChecked.ShouldBe(true);
                form.PlainHttp.IsChecked.ShouldBe(false);
                foreach (var label in new[] { ModelsCopy.FieldDisplayName, ModelsCopy.FieldId, ModelsCopy.FieldProtocol, ModelsCopy.FieldEndpoint, ModelsCopy.FieldAuth, ModelsCopy.FieldModels, ModelsCopy.CapabilityChat, ModelsCopy.CapabilityResponses, ModelsCopy.CapabilityEmbeddings, ModelsCopy.FieldAllowPlainHttp })
                {
                    SettingsTree.Visible<Control>(form).Any(control => string.Equals(AutomationProperties.GetName(control), label, StringComparison.Ordinal)).ShouldBeTrue(label);
                }

                view.Templates.IsEffectivelyVisible.ShouldBeFalse("the templates give way to the open form");
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Saving a blank form sends nothing and shows each missing entry's message directly under its own field.</summary>
    [Fact]
    public async Task SavingABlankFormShowsEachMessageUnderItsField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                OpenForm(fixture, view);

                SettingsTree.Click(fixture.Window, view.SaveDraft);

                Messages(view).ShouldBe([ModelsCopy.ErrDisplayNameRequired, ModelsCopy.ErrIdRequired, ModelsCopy.ErrEndpointRequired]);
                fixture.ModelsSource.SavedForms.ShouldBeEmpty();
                fixture.Models.IsDraftOpen.ShouldBeTrue();
                var texts = SettingsTree.Texts(view.Form).ToList();
                texts.IndexOf(ModelsCopy.FieldDisplayName).ShouldBeLessThan(texts.IndexOf(ModelsCopy.ErrDisplayNameRequired));
                texts.IndexOf(ModelsCopy.ErrDisplayNameRequired).ShouldBeLessThan(texts.IndexOf(ModelsCopy.FieldId));
                texts.IndexOf(ModelsCopy.FieldId).ShouldBeLessThan(texts.IndexOf(ModelsCopy.ErrIdRequired));
                texts.IndexOf(ModelsCopy.ErrIdRequired).ShouldBeLessThan(texts.IndexOf(ModelsCopy.FieldProtocol));
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Each entry is checked as it is typed: the id, the endpoint (with the plain http switch), the header name and the model ids.</summary>
    [Fact]
    public async Task EntriesAreCheckedAsTheyAreTyped()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                OpenForm(fixture, view);
                var form = view.Form;

                form.Id.Text = "Bad Id";
                Messages(view).ShouldBe([ModelsCopy.ErrIdInvalid]);
                form.Id.Text = "ollama";
                Messages(view).ShouldBe([ModelsCopy.ErrIdTaken]);
                form.Id.Text = "fresh";
                Messages(view).ShouldBeEmpty();

                form.Endpoint.Text = "http://llm.example.test/v1";
                Messages(view).ShouldBe([ModelsCopy.ErrEndpointPlainHttpRemote]);
                form.PlainHttp.IsChecked = true;
                Messages(view).ShouldBeEmpty("the switch allows plain http to a remote host");
                form.PlainHttp.IsChecked = false;
                form.Endpoint.Text = "ftp://llm.example.test/v1";
                Messages(view).ShouldBe([ModelsCopy.ErrEndpointUnsupportedScheme]);
                form.Endpoint.Text = "https://llm.example.test/v1";
                Messages(view).ShouldBeEmpty();

                Choose(form.Auth, CustomProviderAuth.None);
                Messages(view).ShouldBeEmpty("no authentication is a valid choice");
                Choose(form.Auth, CustomProviderAuth.Bearer);
                Messages(view).ShouldBeEmpty();
                SettingsTree.Visible<Control>(form)
                    .Any(control => AutomationProperties.GetName(control)?.Contains("header", StringComparison.OrdinalIgnoreCase) == true)
                    .ShouldBeFalse("omp's models.yml cannot carry an authentication header name, so the form offers none");

                form.Models.Text = "a b";
                Messages(view).ShouldBe([ModelsCopy.ErrModelInvalid]);
                form.Models.Text = "alpha\nalpha";
                Messages(view).ShouldBe([ModelsCopy.ErrModelDuplicate]);
                form.Models.Text = "alpha\nbeta";
                Messages(view).ShouldBeEmpty();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A valid form is written through the source as typed, closes, and the saved provider appears with its own key action.</summary>
    [Fact]
    public async Task AValidFormIsSavedAndTheNewProviderAppearsWithAKeyAction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                OpenForm(fixture, view);
                var form = view.Form;
                form.DisplayName.Text = "Local Llama";
                form.Id.Text = "local-llama";
                form.Endpoint.Text = "http://localhost:8080/v1";
                form.Models.Text = "llama-3\nllama-3-8b";
                form.Chat.IsChecked = false;
                SettingsTree.Named<CheckBox>(form, ModelsCopy.CapabilityResponses).IsChecked = true;
                SettingsTree.Named<CheckBox>(form, ModelsCopy.CapabilityEmbeddings).IsChecked = true;
                Choose(form.Protocol, CustomProviderProtocol.AnthropicCompatible);
                fixture.Settle();
                SettingsTree.Texts(form).ShouldContain(ModelsCopy.EndpointHint(CustomProviderProtocol.AnthropicCompatible), StringComparer.Ordinal);

                SettingsTree.Click(fixture.Window, view.SaveDraft);

                var saved = fixture.ModelsSource.SavedForms.ShouldHaveSingleItem();
                saved.DisplayName.ShouldBe("Local Llama");
                saved.Id.ShouldBe("local-llama");
                saved.Protocol.ShouldBe(CustomProviderProtocol.AnthropicCompatible);
                saved.BaseEndpoint.ShouldBe(new Uri("http://localhost:8080/v1"));
                saved.Auth.ShouldBe(CustomProviderAuth.ApiKey);
                saved.Models.ShouldBe(["llama-3", "llama-3-8b"]);
                saved.Capabilities.ShouldBe(CustomProviderCapabilities.Responses | CustomProviderCapabilities.Embeddings);
                saved.AllowRemotePlainHttp.ShouldBeFalse();

                fixture.Models.IsDraftOpen.ShouldBeFalse();
                view.Form.IsEffectivelyVisible.ShouldBeFalse();
                var card = ModelsPageViewTests.Card(view, "local-llama");
                card.ApiKey.IsEffectivelyVisible.ShouldBeTrue("the key is added on the saved provider's card");
                view.Live.Text.ShouldBe(ModelsCopy.CustomSaved("Local Llama"));
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>When omp refuses the form it stays open with omp's reason beside Save; Cancel closes it without writing.</summary>
    [Fact]
    public async Task ARefusedFormStaysOpenWithTheReasonAndCancelClosesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct, static f => f.ModelsSource.SaveCustomResult = ModelsActionResult.Failure("models.yml is read-only.")).ConfigureAwait(true);
            using (fixture)
            {
                OpenForm(fixture, view);
                var form = view.Form;
                form.DisplayName.Text = "Local Llama";
                form.Id.Text = "local-llama";
                form.Endpoint.Text = "http://localhost:8080/v1";

                SettingsTree.Click(fixture.Window, view.SaveDraft);

                fixture.Models.IsDraftOpen.ShouldBeTrue();
                view.DraftError.IsEffectivelyVisible.ShouldBeTrue();
                view.DraftError.Text.ShouldBe("models.yml is read-only.");
                AutomationProperties.GetLiveSetting(view.DraftError).ShouldBe(AutomationLiveSetting.Assertive);

                SettingsTree.Click(fixture.Window, view.CancelDraft);

                fixture.Models.IsDraftOpen.ShouldBeFalse();
                view.Form.IsEffectivelyVisible.ShouldBeFalse();
                fixture.Model.HasUnsavedChanges.ShouldBeFalse();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Edit opens the saved provider in the form with its id fixed, and saving writes the same id.</summary>
    [Fact]
    public async Task EditOpensTheSavedProviderWithItsIdFixed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "mine").Edit);

                var form = view.Form;
                SettingsTree.Texts(form).ShouldContain(ModelsCopy.DraftTitleEdit, StringComparer.Ordinal);
                form.DisplayName.Text.ShouldBe("My Gateway");
                form.Id.Text.ShouldBe("mine");
                form.Id.IsEnabled.ShouldBeFalse("the id of a saved provider cannot change");
                form.Endpoint.Text.ShouldBe("https://llm.example.test/v1");
                form.Models.Text.ShouldBe("mine/small");
                fixture.Model.HasUnsavedChanges.ShouldBeFalse("an untouched form is not an unsaved change");

                form.DisplayName.Text = "My Gateway 2";
                fixture.Model.HasUnsavedChanges.ShouldBeTrue();
                SettingsTree.Click(fixture.Window, view.SaveDraft);

                fixture.ModelsSource.SavedForms.ShouldHaveSingleItem().Id.ShouldBe("mine");
                ModelsPageViewTests.Card(view, "mine").Card!.DisplayName.ShouldBe("My Gateway 2");
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Remove asks first, in place; Keep provider drops the question and nothing is removed; Remove provider removes it.</summary>
    [Fact]
    public async Task RemoveAsksInPlaceBeforeRemoving()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (fixture, view) = await OpenAsync(ct).ConfigureAwait(true);
            using (fixture)
            {
                var card = ModelsPageViewTests.Card(view, "mine");
                SettingsTree.Click(fixture.Window, card.Remove);

                card.RemoveQuestion.IsEffectivelyVisible.ShouldBeTrue();
                card.RemoveQuestion.Text.ShouldBe(ModelsCopy.RemoveQuestion("My Gateway"));
                card.Remove.IsEffectivelyVisible.ShouldBeFalse();
                card.Keep.IsFocused.ShouldBeTrue("focus lands on the choice that changes nothing");
                AutomationProperties.GetName(card.ConfirmRemove).ShouldBe(SettingsViewCopy.ForProvider(ModelsCopy.ConfirmRemoveLabel, "My Gateway"));
                fixture.ModelsSource.Removed.ShouldBeEmpty();

                SettingsTree.Click(fixture.Window, card.Keep);

                card.RemoveQuestion.IsEffectivelyVisible.ShouldBeFalse();
                card.Remove.IsEffectivelyVisible.ShouldBeTrue();
                card.Remove.IsFocused.ShouldBeTrue("focus returns to the button that asked");
                fixture.ModelsSource.Removed.ShouldBeEmpty();

                SettingsTree.Click(fixture.Window, card.Remove);
                SettingsTree.Click(fixture.Window, card.ConfirmRemove);

                fixture.ModelsSource.Removed.ShouldBe(["mine"]);
                view.CardViews.Select(static candidate => candidate.Card!.Id).ShouldNotContain("mine", StringComparer.Ordinal);
                view.Live.Text.ShouldBe(ModelsCopy.CustomRemoved("My Gateway"));
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The template list offers each template with its words and a Use template button, and choosing one opens the form filled in with where it came from.</summary>
    [Fact]
    public async Task ChoosingATemplatePrefillsTheForm()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            ProviderTemplate[] templates = [ModelsData.Template("lm-studio", "LM Studio", "http://localhost:1234/v1"), ModelsData.Template("vllm", "vLLM", null)];
            var (fixture, view) = await OpenAsync(ct, f => f.ModelsSource.Snapshot = ModelsData.Snapshot() with { Templates = templates }).ConfigureAwait(true);
            using (fixture)
            {
                view.Templates.IsEffectivelyVisible.ShouldBeTrue();
                view.Templates.IsExpanded = true;
                fixture.Settle();

                var rows = SettingsTree.Texts(view.Templates);
                rows.ShouldContain(ModelsCopy.TemplatesExplanation, StringComparer.Ordinal);
                rows.ShouldContain("LM Studio", StringComparer.Ordinal);
                rows.ShouldContain(ModelsCopy.GroupTitle(ProviderGroup.Local), StringComparer.Ordinal);
                rows.ShouldContain(ModelsCopy.ProtocolLabel(CustomProviderProtocol.OpenAiCompatible), StringComparer.Ordinal);
                rows.ShouldContain(ModelsCopy.TemplateEndpointText(new Uri("http://localhost:1234/v1")), StringComparer.Ordinal);
                rows.ShouldContain(ModelsCopy.TemplateEndpointText(null), StringComparer.Ordinal);
                rows.ShouldContain("Start the server first.", StringComparer.Ordinal);
                view.UseTemplateButtons.Select(static button => AutomationProperties.GetName(button))
                    .ShouldBe([ModelsCopy.UseTemplateName("LM Studio"), ModelsCopy.UseTemplateName("vLLM")]);

                SettingsTree.Click(fixture.Window, view.UseTemplateButtons[0]);

                var form = view.Form;
                var draft = fixture.Models.Draft.ShouldNotBeNull();
                form.IsEffectivelyVisible.ShouldBeTrue();
                form.DisplayName.Text.ShouldBe("LM Studio");
                form.Id.Text.ShouldBe(draft.Id);
                draft.Id.ShouldNotBeNullOrEmpty();
                form.Endpoint.Text.ShouldBe("http://localhost:1234/v1");
                form.Models.Text.ShouldBeNullOrEmpty();
                form.PlainHttp.IsChecked.ShouldBe(false);
                form.TemplateNoteText.IsEffectivelyVisible.ShouldBeTrue();
                form.TemplateNoteText.Text.ShouldBe(draft.TemplateNote);
                form.TemplateNoteText.Text.ShouldNotBeNull().ShouldContain("LM Studio");
                SettingsTree.Texts(form).ShouldContain("Start the server first.", StringComparer.Ordinal);
                view.Templates.IsEffectivelyVisible.ShouldBeFalse();

                form.Models.Text = "qwen-7b";
                SettingsTree.Click(fixture.Window, view.SaveDraft);

                var saved = fixture.ModelsSource.SavedForms.ShouldHaveSingleItem();
                saved.DisplayName.ShouldBe("LM Studio");
                saved.Models.ShouldBe(["qwen-7b"]);
                saved.BaseEndpoint.ShouldBe(new Uri("http://localhost:1234/v1"));
                view.Templates.IsEffectivelyVisible.ShouldBeTrue("the templates return when the form closes");
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A template without an endpoint leaves the endpoint for the user to enter, and does not accept an empty one.</summary>
    [Fact]
    public async Task ATemplateWithoutAnEndpointLeavesItBlank()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            ProviderTemplate[] templates = [ModelsData.Template("vllm", "vLLM", null)];
            var (fixture, view) = await OpenAsync(ct, f => f.ModelsSource.Snapshot = ModelsData.Snapshot() with { Templates = templates }).ConfigureAwait(true);
            using (fixture)
            {
                view.Templates.IsExpanded = true;
                fixture.Settle();

                SettingsTree.Click(fixture.Window, view.UseTemplateButtons.ShouldHaveSingleItem());
                view.Form.Endpoint.Text.ShouldBeNullOrEmpty();
                SettingsTree.Click(fixture.Window, view.SaveDraft);

                Messages(view).ShouldContain(ModelsCopy.ErrEndpointRequired, StringComparer.Ordinal);
                fixture.ModelsSource.SavedForms.ShouldBeEmpty();
            }
        }, ct).ConfigureAwait(true);
    }
}
