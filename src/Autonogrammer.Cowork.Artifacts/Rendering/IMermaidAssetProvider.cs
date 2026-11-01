namespace Autonogrammer.Cowork.Artifacts.Rendering;

/// <summary>Supplies the vendored Mermaid bundle. Tests use a fake; the real bundle is wired later.</summary>
public interface IMermaidAssetProvider
{
    /// <summary>Returns the Mermaid script text, or null when the bundle is missing.</summary>
    /// <returns>The script, or null.</returns>
    string? GetScript();
}
