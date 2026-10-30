using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Harness.PluginBundles;

namespace Autonogrammer.Native.Composition.Plugins;

/// <summary>
/// Feeds bundle <c>.mcp.json</c> servers into <see cref="McpServerCatalog"/> as switched-off entries.
/// NotificationRequest and INotificationSink live in Harness.Notifications. Presentation never references them:
/// the bridge sits in Native and Native.Views.
/// </summary>
public sealed class BundleMcpExtraSource : IMcpExtraServerSource
{
    /// <summary>A priority above every file location, so a file of the same name wins.</summary>
    public const int BundlePriority = 1_000_000;

    private readonly IReadOnlyList<string> _bundleDirectories;
    private readonly bool _projectScope;

    /// <summary>Creates a source over installed bundle directories.</summary>
    /// <param name="bundleDirectories">Installed bundle roots. Each may contain <c>.mcp.json</c>.</param>
    /// <param name="projectScope">True when the bundles were installed for one project, so consent applies.</param>
    public BundleMcpExtraSource(IReadOnlyList<string> bundleDirectories, bool projectScope)
    {
        ArgumentNullException.ThrowIfNull(bundleDirectories);
        _bundleDirectories = bundleDirectories;
        _projectScope = projectScope;
    }

    /// <inheritdoc />
    public IReadOnlyList<McpServerSpec> Servers(McpLocatorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var servers = new List<McpServerSpec>();
        foreach (var directory in _bundleDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            var pluginId = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                continue;
            }

            foreach (var descriptor in BundleMcpSource.Read(directory, pluginId))
            {
                servers.Add(ToSpec(descriptor));
            }
        }

        return servers;
    }

    private McpServerSpec ToSpec(BundleMcpServerDescriptor descriptor)
    {
        var args = descriptor.Arguments;
        var env = descriptor.Environment.ToDictionary(static pair => pair.Key, static pair => pair.Value ?? string.Empty, StringComparer.Ordinal);
        var preview = string.IsNullOrWhiteSpace(descriptor.Command)
            ? descriptor.Name
            : descriptor.Command + (args.Count == 0 ? string.Empty : " " + string.Join(' ', args));
        return new McpServerSpec
        {
            Name = descriptor.Name,
            Transport = McpTransportKind.Stdio,
            Scope = _projectScope ? McpScope.Project : McpScope.User,
            Format = McpConfigFormat.ClaudeProject,
            Origin = "plugin:" + descriptor.OriginBundle,
            Priority = BundlePriority,
            Command = descriptor.Command,
            Args = args,
            Env = env,
            EnabledInFile = false,
            CommandPreview = preview,
            ConsentIdentity = McpConsentIdentity.Compute(McpTransportKind.Stdio, descriptor.Command, args, env, null, new Dictionary<string, string>(StringComparer.Ordinal)),
        };
    }
}
