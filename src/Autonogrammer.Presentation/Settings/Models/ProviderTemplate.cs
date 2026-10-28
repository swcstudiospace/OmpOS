namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>
/// A catalogue row that is not a provider omp knows: a starting point for a custom provider. omp only knows its built-in providers
/// and the providers in its <c>models.yml</c>, so a template (Ollama, LM Studio, vLLM, a hosted endpoint, a coding plan) is picked,
/// completed with model ids, saved as a custom provider, and only then gets its API key on the saved provider's card. A template
/// never holds a key.
/// </summary>
/// <param name="Id">The stable template id; the base of the new provider's id slug.</param>
/// <param name="DisplayName">The name shown for the template and given to the new provider.</param>
/// <param name="Group">The catalogue group the template belongs to: <see cref="ProviderGroup.Local"/>, <see cref="ProviderGroup.Cloud"/> or <see cref="ProviderGroup.CodingPlan"/>.</param>
/// <param name="Protocol">The wire protocol of the endpoint.</param>
/// <param name="BaseEndpoint">The base endpoint, or null when the user has to supply one.</param>
/// <param name="Auth">How the endpoint authenticates.</param>
/// <param name="Capabilities">The routes the endpoint supports.</param>
/// <param name="SourceUrl">The page the row was verified on, or null.</param>
/// <param name="RetrievedOn">The date that page was read, or null.</param>
/// <param name="Plan">What the catalogue says about the plan for a coding plan row, or null.</param>
/// <param name="Note">A sentence of advice about the template (for example to start the server first), or null.</param>
public sealed record ProviderTemplate(
    string Id,
    string DisplayName,
    ProviderGroup Group,
    CustomProviderProtocol Protocol,
    Uri? BaseEndpoint,
    CustomProviderAuth Auth,
    CustomProviderCapabilities Capabilities,
    Uri? SourceUrl,
    DateOnly? RetrievedOn,
    ProviderPlanInfo? Plan,
    string? Note);
