namespace Autonogrammer.Api.Accounts;

/// <summary>Marks account routes for the disabled-feature middleware.</summary>
internal sealed class AccountRouteMarker
{
    private AccountRouteMarker()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static AccountRouteMarker Instance { get; } = new();
}
