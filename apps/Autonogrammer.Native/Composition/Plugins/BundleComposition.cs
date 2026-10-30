using Autonogrammer.Harness.PluginBundles;

namespace Autonogrammer.Native.Composition.Plugins;

/// <summary>
/// Registers bundle skills, commands and MCP servers for one head. Remote sessions load no bundles.
/// Bundle items show under the project label. NotificationRequest and INotificationSink live in
/// Harness.Notifications. Presentation never references them: the bridge sits in Native and Native.Views.
/// </summary>
public sealed class BundleComposition
{
    /// <summary>Gets or sets the roots convention loaders append. The argument is true for a remote session, which loads no bundles.</summary>
    public static Func<bool, IReadOnlyList<string>>? PluginRootsFor { get; set; }

    /// <summary>Creates the composition over one plugins root and one state directory.</summary>
    /// <param name="pluginsRoot">Where bundles are installed.</param>
    /// <param name="stateDirectory">Where enable flags are stored.</param>
    /// <param name="projectScope">True for a project install root.</param>
    public BundleComposition(string pluginsRoot, string stateDirectory, bool projectScope = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        PluginsRoot = pluginsRoot;
        Bundles = new BundleStateStore(Path.Combine(stateDirectory, "bundle-state.json"));
        Skills = new SkillStateStore(Path.Combine(stateDirectory, "skill-state.json"));
        Contributor = new BundleLaunchContributor([pluginsRoot], Skills, Bundles);
        Management = new BundleManagementSource(new BundleInstaller(), Bundles, Skills, pluginsRoot, projectScope ? BundleInstallScope.Project : BundleInstallScope.User);
        ExtraServers = new BundleMcpExtraSource(EnabledBundleDirectories(), projectScope);
        ProjectScope = projectScope;
    }

    /// <summary>Gets the plugins root.</summary>
    public string PluginsRoot { get; }

    /// <summary>Gets the bundle enable store.</summary>
    public BundleStateStore Bundles { get; }

    /// <summary>Gets the skill enable store.</summary>
    public SkillStateStore Skills { get; }

    /// <summary>Gets the launch contributor. It adds overlay entries only.</summary>
    public BundleLaunchContributor Contributor { get; }

    /// <summary>Gets the settings adapter.</summary>
    public BundleManagementSource Management { get; }

    /// <summary>Gets the catalog feed. Servers arrive switched off.</summary>
    public BundleMcpExtraSource ExtraServers { get; }

    /// <summary>Gets whether installs are project-scoped.</summary>
    public bool ProjectScope { get; }

    /// <summary>Gets enabled bundle directories for <c>ConventionSources.PluginRoots</c>. Remote sessions pass none.</summary>
    /// <param name="remote">True when the session is remote. Remote sessions load no bundles.</param>
    /// <returns>The bundle roots, or empty for a remote session.</returns>
    public IReadOnlyList<string> PluginRoots(bool remote = false) => remote ? [] : EnabledBundleDirectories();

    private IReadOnlyList<string> EnabledBundleDirectories()
    {
        if (!Directory.Exists(PluginsRoot))
        {
            return [];
        }

        var roots = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(PluginsRoot).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(directory);
            if (id.StartsWith('.') || (Bundles.TryGetEnabled(id, out var enabled) && !enabled))
            {
                continue;
            }

            roots.Add(directory);
        }

        return roots;
    }
}
