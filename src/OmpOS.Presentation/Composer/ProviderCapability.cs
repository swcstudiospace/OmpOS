namespace OmpOS.Presentation.Composer;

/// <summary>
/// What the composer needs to know about one model provider: its display name, whether a session on it can change model, and
/// the models it offers. A head adapts its provider catalogue to this record, so Presentation holds no provider assembly.
/// </summary>
/// <param name="ProviderId">The provider identifier, for example the catalogue id.</param>
/// <param name="DisplayName">The name the composer shows for the provider.</param>
/// <param name="CanSwitchModel">Whether a session on this provider can change the model it uses.</param>
/// <param name="Models">The model identifiers the provider offers, compared by ordinal equality.</param>
public sealed record ProviderCapability(string ProviderId, string DisplayName, bool CanSwitchModel, IReadOnlyList<string> Models);
