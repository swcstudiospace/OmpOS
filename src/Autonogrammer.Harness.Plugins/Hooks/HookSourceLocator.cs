using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Finds the hook configuration files that exist, in merge order (design 2.1). Load order is run order is priority:
/// managed, project, user, Claude local, Claude project, Claude user. All sources run; none replaces another.
/// </summary>
public static class HookSourceLocator
{
    private const int RootHashLength = 12;

    /// <summary>
    /// Returns the managed file path of a platform.
    /// </summary>
    /// <param name="platform">The operating system family.</param>
    /// <param name="programData">The Windows ProgramData directory.</param>
    /// <returns>The path of the Autonogrammer managed <c>hooks.json</c>. Cursor's own enterprise paths are never read.</returns>
    public static string ManagedPath(HookPlatform platform, string programData) => platform switch
    {
        HookPlatform.MacOs => Path.Combine("/Library", "Application Support", "Autonogrammer", "hooks.json"),
        HookPlatform.Windows => Path.Combine(programData, "Autonogrammer", "hooks.json"),
        _ => Path.Combine("/etc", "autonogrammer", "hooks.json"),
    };

    /// <summary>
    /// Finds the hook files that exist.
    /// </summary>
    /// <param name="context">The workspace roots, home directory and platform.</param>
    /// <param name="files">Tells which candidate files exist.</param>
    /// <returns>The existing sources in merge order.</returns>
    public static IReadOnlyList<HookSource> Locate(HookLocatorContext context, IHookFileSource files)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(files);
        var sources = new List<HookSource>();
        var home = context.IsRemote ? null : context.HomeDirectory;

        AddManaged(context, files, sources);
        foreach (var root in context.WorkspaceRoots)
        {
            var hash = RootHash(root);
            AddIfExists(files, sources, HookScope.Project, Path.Combine(root, ".autonogrammer", "hooks.json"), HookConfigFormat.HooksJson, root, root, $"autonogrammer-project:{hash}");
            AddIfExists(files, sources, HookScope.Project, Path.Combine(root, ".cursor", "hooks.json"), HookConfigFormat.HooksJson, root, root, $"cursor-project:{hash}");
        }

        if (home is not null)
        {
            AddUser(files, sources, Path.Combine(home, ".autonogrammer"), "autonogrammer-user");
            AddUser(files, sources, Path.Combine(home, ".cursor"), "cursor-user");
        }

        if (context.IncludeThirdPartyConfigs)
        {
            foreach (var root in context.WorkspaceRoots)
            {
                var hash = RootHash(root);
                AddIfExists(files, sources, HookScope.ClaudeLocal, Path.Combine(root, ".claude", "settings.local.json"), HookConfigFormat.ClaudeSettings, root, root, $"claude-local:{hash}");
            }

            foreach (var root in context.WorkspaceRoots)
            {
                var hash = RootHash(root);
                AddIfExists(files, sources, HookScope.ClaudeProject, Path.Combine(root, ".claude", "settings.json"), HookConfigFormat.ClaudeSettings, root, root, $"claude-project:{hash}");
            }

            if (home is not null)
            {
                var claudeDirectory = Path.Combine(home, ".claude");
                AddIfExists(files, sources, HookScope.ClaudeUser, Path.Combine(claudeDirectory, "settings.json"), HookConfigFormat.ClaudeSettings, claudeDirectory, claudeDirectory, "claude-user");
            }
        }

        return [.. sources.Select((source, index) => source with { Priority = index })];
    }

    /// <summary>
    /// Returns the short stable hash that tells workspace roots apart in a pseudo plugin id.
    /// </summary>
    /// <param name="root">The absolute workspace root.</param>
    /// <returns>Twelve lowercase hex characters.</returns>
    public static string RootHash(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..RootHashLength];
    }

    private static void AddManaged(HookLocatorContext context, IHookFileSource files, List<HookSource> sources)
    {
        var path = ManagedPath(context.Platform, context.ProgramDataDirectory);
        var directory = Path.GetDirectoryName(path) ?? path;
        AddIfExists(files, sources, HookScope.Managed, path, HookConfigFormat.HooksJson, directory, directory, "managed");
    }

    private static void AddUser(IHookFileSource files, List<HookSource> sources, string directory, string pluginId) =>
        AddIfExists(files, sources, HookScope.User, Path.Combine(directory, "hooks.json"), HookConfigFormat.HooksJson, directory, directory, pluginId);

    private static void AddIfExists(
        IHookFileSource files,
        List<HookSource> sources,
        HookScope scope,
        string path,
        HookConfigFormat format,
        string workingDirectory,
        string scopeRoot,
        string pluginId)
    {
        if (files.Inspect(path).Exists)
        {
            sources.Add(new HookSource(scope, path, format, workingDirectory, scopeRoot, pluginId, 0));
        }
    }
}
