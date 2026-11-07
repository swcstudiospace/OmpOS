namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>The manifest row the acquisition dialog names. Null means the install action stays hidden.</summary>
/// <param name="Version">The Chromium version.</param>
/// <param name="SourceHost">The archive URL host.</param>
/// <param name="SizeBytes">The archive size in bytes.</param>
/// <param name="LicenceLine">The licence line shown before download.</param>
public sealed record BrowserUseManifestOffer(string Version, string SourceHost, long SizeBytes, string LicenceLine);
