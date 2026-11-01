using System.Text.Json;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Reads <c>.mcp.json</c> into namespace-qualified descriptors that stay off until confirmed.</summary>
public static class BundleMcpSource
{
    /// <summary>The block text while a server is unconfirmed.</summary>
    public const string NotConfirmed = "BundleServerNotConfirmed";

    private const string RootToken = "${CLAUDE_PLUGIN_ROOT}";

    /// <summary>Reads the servers of one installed bundle.</summary>
    /// <param name="bundleDirectory">The installed bundle. The plugin-root token expands to this directory.</param>
    /// <param name="pluginId">The bundle id.</param>
    /// <returns>The descriptors. An absent file yields an empty list.</returns>
    /// <exception cref="BundleValidationException">The file is present and malformed.</exception>
    public static IReadOnlyList<BundleMcpServerDescriptor> Read(string bundleDirectory, string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var path = Path.Combine(bundleDirectory, ".mcp.json");
        if (!File.Exists(path))
        {
            return [];
        }

        BundleMcpFileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(File.ReadAllText(path), BundleInstallJsonContext.Default.BundleMcpFileDocument);
        }
        catch (JsonException)
        {
            throw new BundleValidationException(BundleInvalidCode.BundleBadManifest, ".mcp.json");
        }

        if (document is null)
        {
            throw new BundleValidationException(BundleInvalidCode.BundleBadManifest, ".mcp.json");
        }

        var root = Path.GetFullPath(bundleDirectory);
        var servers = new List<BundleMcpServerDescriptor>();
        foreach (var pair in document.McpServers.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            var server = pair.Value;
            servers.Add(new BundleMcpServerDescriptor(
                pluginId + "." + pair.Key,
                Expand(server.Command, root) ?? string.Empty,
                server.Args is null ? [] : [.. server.Args.Select(argument => Expand(argument, root) ?? string.Empty)],
                server.Env is null
                    ? []
                    : [.. server.Env.OrderBy(static item => item.Key, StringComparer.Ordinal).Select(item => new BundleMcpEnvPair(item.Key, Expand(item.Value, root)))],
                Expand(server.Cwd, root),
                pluginId,
                Confirmed: false,
                NotConfirmed));
        }

        return servers;
    }

    private static string? Expand(string? value, string root) => value?.Replace(RootToken, root, StringComparison.Ordinal);
}
