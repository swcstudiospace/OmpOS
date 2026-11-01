namespace Autonogrammer.Api.Dispatch;

/// <summary>Endpoint metadata that marks a dispatch route so <see cref="DispatchDisabledMiddleware"/> can answer while the feature is off.</summary>
internal sealed class DispatchRouteMarker
{
    private DispatchRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static DispatchRouteMarker Instance { get; } = new();
}
