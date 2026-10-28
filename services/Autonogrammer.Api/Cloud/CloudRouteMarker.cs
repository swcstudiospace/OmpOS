namespace Autonogrammer.Api.Cloud;

/// <summary>Endpoint metadata that marks a route as part of the cloud control plane, so <see cref="CloudDisabledMiddleware"/> can answer for all of them while cloud is off.</summary>
internal sealed class CloudRouteMarker
{
    private CloudRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static CloudRouteMarker Instance { get; } = new();
}
