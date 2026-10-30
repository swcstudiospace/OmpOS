namespace Autonogrammer.Harness.PluginBundles;

/// <summary>A hooks file that is listed and never run.</summary>
/// <param name="RelativePath">The path inside the bundle.</param>
/// <param name="Content">The file text, listed only.</param>
/// <param name="State">Always <c>BundleComponentNotLoaded(hooks)</c>.</param>
public sealed record BundleHookListing(string RelativePath, string Content, string State);
