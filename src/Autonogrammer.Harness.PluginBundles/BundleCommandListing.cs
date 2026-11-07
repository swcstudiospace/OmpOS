namespace Autonogrammer.Harness.PluginBundles;

/// <summary>A slash command contributed by a bundle. The palette id is <c>/plugin:command</c>.</summary>
/// <param name="PaletteId">The palette id, including the leading slash.</param>
/// <param name="RelativePath">The path inside the bundle.</param>
/// <param name="Suppressed">Whether a built-in command of the same stem wins.</param>
/// <param name="Report">The collision text, or <see langword="null"/>.</param>
public sealed record BundleCommandListing(string PaletteId, string RelativePath, bool Suppressed, string? Report);
