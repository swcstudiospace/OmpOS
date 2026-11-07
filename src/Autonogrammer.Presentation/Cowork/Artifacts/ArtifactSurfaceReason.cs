namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>Why a surface is unavailable. Unavailable shows the source view with the reason.</summary>
public enum ArtifactSurfaceReason
{
    /// <summary>The surface is not unavailable.</summary>
    None,

    /// <summary>The vendored Mermaid bundle is absent or fails its hash check (D-25).</summary>
    MermaidAssetMissing,

    /// <summary>The desktop web view cannot be created on this host.</summary>
    SurfaceUnavailable,

    /// <summary>The host refused the document under the document policy.</summary>
    DocumentRefused,
}
