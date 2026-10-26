using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Tests.Settings;

/// <summary>Provider data the Models page tests read. None of it is a real credential: the keys the tests type are marked fakes.</summary>
internal static class ModelsData
{
    /// <summary>The fake key the tests type into the key entry. It must never be found in any control afterwards.</summary>
    internal const string FakeKey = "sk-fake-not-a-real-key-123456";

    /// <summary>The model selector that is the default in <see cref="Snapshot"/>.</summary>
    internal const string DefaultSelector = "openai/gpt-4o";

    /// <summary>Builds a provider the way a source lists it.</summary>
    /// <param name="id">The provider id.</param>
    /// <param name="name">The display name.</param>
    /// <param name="group">The group.</param>
    /// <param name="auth">How the provider authenticates.</param>
    /// <param name="credential">Whether omp holds a credential.</param>
    /// <param name="models">The model selectors.</param>
    /// <param name="endpoint">The endpoint text, or empty.</param>
    /// <param name="managed">Whether OmpOS wrote the provider.</param>
    /// <param name="plan">The plan, for a coding plan.</param>
    /// <param name="definition">The saved definition of a managed custom provider.</param>
    /// <returns>The provider.</returns>
    internal static ProviderInfo Provider(
        string id,
        string name,
        ProviderGroup group,
        ProviderAuthKind auth,
        CredentialState credential,
        IReadOnlyList<string> models,
        string endpoint = "",
        bool managed = false,
        ProviderPlanInfo? plan = null,
        CustomProviderForm? definition = null) =>
        new(
            id,
            name,
            group,
            endpoint,
            auth,
            credential,
            models,
            plan is null ? null : new Uri("https://plans.example.test/pricing"),
            plan is null ? null : new DateOnly(2026, 10, 22),
            plan,
            managed,
            null,
            definition);

    /// <summary>Builds the form of a managed custom provider.</summary>
    /// <param name="id">The provider id.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The form.</returns>
    internal static CustomProviderForm Form(string id, string name) =>
        new(
            name,
            id,
            CustomProviderProtocol.OpenAiCompatible,
            new Uri("https://llm.example.test/v1"),
            CustomProviderAuth.ApiKey,
            [id + "/small"],
            CustomProviderCapabilities.Chat,
            false);

    /// <summary>Builds the managed custom provider a saved form becomes.</summary>
    /// <param name="form">The saved form.</param>
    /// <returns>The provider.</returns>
    internal static ProviderInfo Saved(CustomProviderForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return Provider(
            form.Id,
            form.DisplayName,
            ProviderGroup.Custom,
            form.Auth == CustomProviderAuth.None ? ProviderAuthKind.None : ProviderAuthKind.ApiKey,
            form.Auth == CustomProviderAuth.None ? CredentialState.NotRequired : CredentialState.Absent,
            [.. form.Models.Select(model => form.Id + "/" + model)],
            form.BaseEndpoint.OriginalString,
            managed: true,
            definition: form);
    }

    /// <summary>Builds a template.</summary>
    /// <param name="id">The template id.</param>
    /// <param name="name">The display name.</param>
    /// <param name="endpoint">The endpoint, or null when the user enters one.</param>
    /// <returns>The template.</returns>
    internal static ProviderTemplate Template(string id, string name, string? endpoint) =>
        new(
            id,
            name,
            ProviderGroup.Local,
            CustomProviderProtocol.OpenAiCompatible,
            endpoint is null ? null : new Uri(endpoint),
            CustomProviderAuth.None,
            CustomProviderCapabilities.Chat,
            new Uri("https://docs.example.test/" + id),
            new DateOnly(2026, 10, 22),
            null,
            "Start the server first.");

    /// <summary>Builds a snapshot with one provider in every group.</summary>
    /// <returns>The snapshot. It lists a local runtime, a cloud provider, a coding plan, a managed custom provider and two built-in providers.</returns>
    internal static ModelsSnapshot Snapshot() =>
        new(
            [
                Provider("ollama", "Ollama", ProviderGroup.Local, ProviderAuthKind.None, CredentialState.NotRequired, ["ollama/llama3"], "http://localhost:11434/v1"),
                Provider("openrouter", "OpenRouter", ProviderGroup.Cloud, ProviderAuthKind.ApiKey, CredentialState.Absent, ["openrouter/alpha", "openrouter/beta"], "https://openrouter.example.test/v1"),
                Provider(
                    "zai-plan",
                    "Z.ai Coding Plan",
                    ProviderGroup.CodingPlan,
                    ProviderAuthKind.ApiKey,
                    CredentialState.Present,
                    ["zai-plan/glm"],
                    plan: new ProviderPlanInfo([new PlanTierInfo("Lite", "about 120 requests per 5 hours"), new PlanTierInfo("Pro", "about 600 requests per 5 hours")], 30, new Uri("https://plans.example.test/pricing"))),
                Provider("mine", "My Gateway", ProviderGroup.Custom, ProviderAuthKind.ApiKey, CredentialState.Present, ["mine/small"], "https://llm.example.test/v1", managed: true, definition: Form("mine", "My Gateway")),
                Provider("openai", "OpenAI", ProviderGroup.OmpBuiltIn, ProviderAuthKind.ApiKey, CredentialState.Present, [DefaultSelector, "openai/gpt-4o-mini"]),
                Provider("anthropic", "Anthropic", ProviderGroup.OmpBuiltIn, ProviderAuthKind.OAuth, CredentialState.Absent, []),
            ],
            DefaultSelector,
            null,
            true);
}
