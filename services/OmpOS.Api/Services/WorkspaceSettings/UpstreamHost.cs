using System.Data.Common;
using System.Globalization;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>Reduces a configured address to host or host:port. Nothing else of the address survives: no scheme, path, query, user name or password.</summary>
internal static class UpstreamHost
{
    private static readonly string[] HostKeys = ["Host", "Server", "Data Source"];

    /// <summary>Reads the host of a URL.</summary>
    /// <param name="address">The configured URL.</param>
    /// <returns>The host, with the port when it is not the scheme's default, or null when the value is not an absolute URL.</returns>
    public static string? FromUrl(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        return uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Reads the host of an ADO-style connection string (<c>Host=...;Port=...;Password=...</c>).</summary>
    /// <param name="connectionString">The configured connection string.</param>
    /// <returns>The host and port, or null when the value has no host key or is not a connection string.</returns>
    public static string? FromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (var key in HostKeys)
            {
                if (builder.TryGetValue(key, out var value) && value is string { Length: > 0 } host)
                {
                    return builder.TryGetValue("Port", out var port) && port is string { Length: > 0 } number && !host.Contains(':', StringComparison.Ordinal)
                        ? host + ":" + number
                        : host;
                }
            }

            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Reads the first endpoint of a StackExchange.Redis style configuration (<c>host:6379,password=...</c>).</summary>
    /// <param name="configuration">The configured connection string.</param>
    /// <returns>The first host and port, or null when the first element is an option or the value is empty.</returns>
    public static string? FromRedisConfiguration(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return null;
        }

        var first = configuration.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null || first.Contains('=', StringComparison.Ordinal))
        {
            return null;
        }

        var at = first.LastIndexOf('@');
        var host = at >= 0 ? first[(at + 1)..] : first;
        return host.Length == 0 ? null : host;
    }
}
