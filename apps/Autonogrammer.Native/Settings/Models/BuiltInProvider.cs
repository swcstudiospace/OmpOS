namespace Autonogrammer.Native.Settings.Models;

/// <summary>One API-key provider the engine knows by itself, as the Models page lists it before the user has added a key.</summary>
/// <param name="Id">The provider id the engine uses in selectors and in <c>engine auth import</c>.</param>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="KeyPage">The page where the provider issues API keys, as read on <paramref name="ReadOn"/>, or null when that page could not be read.</param>
/// <param name="ReadOn">The date <paramref name="KeyPage"/> was read, or null when <paramref name="KeyPage"/> is null.</param>
internal sealed record BuiltInProvider(string Id, string DisplayName, Uri? KeyPage, DateOnly? ReadOn);
