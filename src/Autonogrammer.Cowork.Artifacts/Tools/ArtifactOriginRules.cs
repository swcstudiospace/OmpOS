using System.Net;
using System.Text.Json;

namespace Autonogrammer.Cowork.Artifacts.Tools;

/// <summary>Validates declared artifact network origins and storage. Origins are requests, not grants.</summary>
public static class ArtifactOriginRules
{
    /// <summary>The maximum number of declared origins.</summary>
    public const int MaxOrigins = 5;

    /// <summary>Reads permissions. Invalid origins are refused. Storage other than <c>none</c> is a warning.</summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="origins">The accepted origins.</param>
    /// <param name="warnings">Notices, including a storage refusal notice.</param>
    /// <param name="error">The typed error when origins are rejected.</param>
    /// <returns>True when the declaration may be stored.</returns>
    public static bool TryRead(JsonElement arguments, out string[] origins, out string[] warnings, out string? error)
    {
        var originList = new List<string>();
        var warningList = new List<string>();
        origins = [];
        warnings = [];
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty("permissions", out var permissions) || permissions.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (permissions.ValueKind != JsonValueKind.Object)
        {
            error = "origin_rejected";
            return false;
        }

        if (permissions.TryGetProperty("storage", out var storage) && storage.ValueKind == JsonValueKind.String && !string.Equals(storage.GetString(), "none", StringComparison.Ordinal))
        {
            warningList.Add("storage_none_only");
        }

        if (!permissions.TryGetProperty("network", out var network) || network.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            warnings = warningList.ToArray();
            return true;
        }

        if (network.ValueKind != JsonValueKind.Array)
        {
            error = "origin_rejected";
            return false;
        }

        foreach (var entry in network.EnumerateArray())
        {
            var origin = entry.ValueKind == JsonValueKind.String
                ? entry.GetString()
                : entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("origin", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
            if (origin is null || !IsAllowed(origin))
            {
                error = "origin_rejected";
                return false;
            }

            originList.Add(origin);
        }

        if (originList.Count > MaxOrigins)
        {
            error = "origin_rejected";
            return false;
        }

        origins = originList.ToArray();
        warnings = warningList.ToArray();
        return true;
    }

    /// <summary>Returns whether an origin is https, host-only, and not a local or metadata address.</summary>
    /// <param name="origin">The declared origin.</param>
    /// <returns>True when the origin may be requested.</returns>
    public static bool IsAllowed(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
        {
            return false;
        }

        var host = uri.IdnHost;
        if (host.Length == 0 || host.Contains('*', StringComparison.Ordinal) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (host.Contains("metadata", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out _))
        {
            return false;
        }

        return true;
    }
}
