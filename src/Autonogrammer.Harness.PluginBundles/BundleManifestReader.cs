using System.Text.Json;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Reads the optional <c>.claude-plugin/plugin.json</c>. Messages name the code and the relative path only.</summary>
public static class BundleManifestReader
{
    /// <summary>The relative path of the optional manifest.</summary>
    public const string RelativePath = ".claude-plugin/plugin.json";

    /// <summary>Reads a manifest file when it exists.</summary>
    /// <param name="bundleDirectory">The bundle root.</param>
    /// <returns>The manifest, or <see langword="null"/> when the file is absent.</returns>
    /// <exception cref="ArgumentException"><paramref name="bundleDirectory"/> is empty.</exception>
    /// <exception cref="BundleValidationException">The file is present and malformed.</exception>
    public static PluginBundleManifest? ReadOptional(string bundleDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        var path = Path.Combine(bundleDirectory, ".claude-plugin", "plugin.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return ReadJson(File.ReadAllText(path), RelativePath);
    }

    /// <summary>Parses manifest JSON.</summary>
    /// <param name="json">The file text.</param>
    /// <param name="relativePath">The path to name if parsing fails.</param>
    /// <returns>The manifest.</returns>
    /// <exception cref="BundleValidationException">The JSON is not a manifest this version accepts.</exception>
    public static PluginBundleManifest ReadJson(string json, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        try
        {
            var manifest = JsonSerializer.Deserialize(json, PluginBundleJsonContext.Default.PluginBundleManifest);
            return manifest ?? throw new BundleValidationException(BundleInvalidCode.BundleBadManifest, relativePath);
        }
        catch (JsonException)
        {
            throw new BundleValidationException(BundleInvalidCode.BundleBadManifest, relativePath);
        }
    }
}
