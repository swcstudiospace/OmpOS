namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>One provider as the Models page shows it. The record holds no key material.</summary>
/// <param name="Id">The provider id omp uses, for example <c>openrouter</c>.</param>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="Group">The group the card sits in.</param>
/// <param name="EndpointText">The endpoint as text, or an empty string when omp owns the endpoint.</param>
/// <param name="AuthKind">How the provider authenticates.</param>
/// <param name="Credential">Whether omp holds a credential for it.</param>
/// <param name="Models">The models the provider offers, as <c>provider/model</c> selectors.</param>
/// <param name="SourceUrl">The page a catalogue row was verified on, or null for rows that are not catalogue rows.</param>
/// <param name="RetrievedOn">The date that page was read, or null when <paramref name="SourceUrl"/> is null.</param>
/// <param name="Plan">The plan description of a coding plan row, or null.</param>
/// <param name="IsAutonogrammerManaged">True when Autonogrammer wrote the provider to <c>models.yml</c> and may edit and remove it.</param>
/// <param name="Note">A short note for the card, or null.</param>
/// <param name="Definition">The saved definition of an Autonogrammer managed custom provider, so it can be edited; null otherwise.</param>
public sealed record ProviderInfo(
    string Id,
    string DisplayName,
    ProviderGroup Group,
    string EndpointText,
    ProviderAuthKind AuthKind,
    CredentialState Credential,
    IReadOnlyList<string> Models,
    Uri? SourceUrl,
    DateOnly? RetrievedOn,
    ProviderPlanInfo? Plan,
    bool IsAutonogrammerManaged,
    string? Note,
    CustomProviderForm? Definition = null);
