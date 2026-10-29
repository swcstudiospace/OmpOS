namespace Autonogrammer.Api.Hosting;

/// <summary>Names shared by the hosting extensions.</summary>
internal static class HostingPolicies
{
    /// <summary>Gets the named request-timeout policy for routes that stream. Apply it with <c>.WithRequestTimeout(HostingPolicies.NoTimeout)</c> or <c>.DisableRequestTimeout()</c>.</summary>
    public const string NoTimeout = "NoTimeout";

    /// <summary>Gets the health-check tag of the startup probe.</summary>
    public const string StartupTag = "startup";

    /// <summary>Gets the health-check tag of the readiness probe.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Gets the configuration key that selects JSON console logs.</summary>
    public const string JsonLogsKey = "Hosting:JsonLogs";
}
