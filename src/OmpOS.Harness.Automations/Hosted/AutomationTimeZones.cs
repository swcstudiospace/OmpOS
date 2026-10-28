using System.Diagnostics.CodeAnalysis;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Resolves the time zone identifier an automation carries.
/// </summary>
public static class AutomationTimeZones
{
    /// <summary>Resolves an identifier to a time zone.</summary>
    /// <param name="id">An IANA identifier such as <c>Europe/Berlin</c>, or <c>UTC</c>.</param>
    /// <param name="zone">The zone when the call succeeds.</param>
    /// <returns>True when the identifier names a zone known to this host.</returns>
    public static bool TryResolve(string? id, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            return false;
        }

        if (string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            zone = TimeZoneInfo.Utc;
            return true;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone);
    }
}
