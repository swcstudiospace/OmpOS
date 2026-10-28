namespace Autonogrammer.Api.Automations;

/// <summary>Endpoint metadata that marks a route as part of the automations feature, so <see cref="AutomationDisabledMiddleware"/> can answer for all of them while automations are off.</summary>
internal sealed class AutomationRouteMarker
{
    private AutomationRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static AutomationRouteMarker Instance { get; } = new();
}
