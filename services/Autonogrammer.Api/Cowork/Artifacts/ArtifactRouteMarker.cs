namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Marks an artifact route so <see cref="ArtifactsDisabledMiddleware"/> can answer while artifacts are off.</summary>
internal sealed class ArtifactRouteMarker
{
    private ArtifactRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static ArtifactRouteMarker Instance { get; } = new();
}
