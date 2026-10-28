namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>
/// A custom provider as saved: the values of a draft that passed every rule. It holds no key material; the key goes to omp
/// through <see cref="IModelsSettingsSource.SaveApiKeyAsync"/>.
/// </summary>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="Id">The slug that names the provider.</param>
/// <param name="Protocol">The protocol the endpoint speaks.</param>
/// <param name="BaseEndpoint">The base address. Plain http is accepted to loopback only unless <paramref name="AllowRemotePlainHttp"/> is set.</param>
/// <param name="Auth">How the endpoint authenticates.</param>
/// <param name="Models">The model ids the provider serves, one per entry, in the order the user listed them.</param>
/// <param name="Capabilities">The routes the user records as supported.</param>
/// <param name="AllowRemotePlainHttp">True when the user explicitly accepts plain http to a non-loopback host.</param>
public sealed record CustomProviderForm(
    string DisplayName,
    string Id,
    CustomProviderProtocol Protocol,
    Uri BaseEndpoint,
    CustomProviderAuth Auth,
    IReadOnlyList<string> Models,
    CustomProviderCapabilities Capabilities,
    bool AllowRemotePlainHttp);
