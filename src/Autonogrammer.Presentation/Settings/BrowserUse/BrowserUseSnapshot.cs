namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>What a load of the Browser page returns: Chromium state, the saved options and the manifest row, if any.</summary>
/// <param name="State">The Chromium state.</param>
/// <param name="Version">The ready binary's major version, or null.</param>
/// <param name="Values">The saved options.</param>
/// <param name="Offer">The platform row, or null when acquisition is hidden.</param>
public sealed record BrowserUseSnapshot(
    BrowserUseChromiumState State,
    string? Version,
    BrowserUseSettingsValues Values,
    BrowserUseManifestOffer? Offer);
