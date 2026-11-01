using Autonogrammer.Harness.PluginBundles;
using Autonogrammer.Presentation.Settings.Plugins;

namespace Autonogrammer.Native.Composition.Plugins;

/// <summary>
/// Native adapter of <see cref="IBundleManagementSource"/> over the harness installer and enable stores.
/// NotificationRequest and INotificationSink live in Harness.Notifications. Presentation never references them:
/// the bridge sits in Native and Native.Views.
/// </summary>
internal sealed class BundleManagementSource : IBundleManagementSource
{
    private readonly BundleInstaller _installer;
    private readonly BundleStateStore _bundles;
    private readonly SkillStateStore _skills;
    private readonly string _destinationRoot;
    private readonly BundleInstallScope _scope;

    /// <summary>Creates the adapter.</summary>
    /// <param name="installer">The installer. It does not start a bundle process.</param>
    /// <param name="bundles">The bundle enable store.</param>
    /// <param name="skills">The skill enable store.</param>
    /// <param name="destinationRoot">The plugins root bundles are installed into.</param>
    /// <param name="scope">User or project scope stored in the sidecar.</param>
    public BundleManagementSource(
        BundleInstaller installer,
        BundleStateStore bundles,
        SkillStateStore skills,
        string destinationRoot,
        BundleInstallScope scope)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(bundles);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        _installer = installer;
        _bundles = bundles;
        _skills = skills;
        _destinationRoot = destinationRoot;
        _scope = scope;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BundleSnapshot>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_destinationRoot))
        {
            return Task.FromResult<IReadOnlyList<BundleSnapshot>>([]);
        }

        var rows = new List<BundleSnapshot>();
        foreach (var directory in Directory.EnumerateDirectories(_destinationRoot).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(directory);
            if (id.StartsWith('.'))
            {
                continue;
            }

            rows.Add(Snapshot(id, directory));
        }

        return Task.FromResult<IReadOnlyList<BundleSnapshot>>(rows);
    }

    /// <inheritdoc />
    public Task<BundleSnapshot> InstallAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var installed = _installer.Install(path, _destinationRoot, _scope);
            var id = Path.GetFileName(installed);
            return Task.FromResult(Snapshot(id, installed));
        }
        catch (BundleValidationException exception)
        {
            throw new BundleManagementException(exception.Code.ToString());
        }
    }

    /// <inheritdoc />
    public Task SetBundleEnabledAsync(string bundleId, bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _bundles.SetEnabled(bundleId, enabled);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetSkillEnabledAsync(string bundleId, string skillId, bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _skills.SetEnabled(bundleId, skillId, enabled);
        return Task.CompletedTask;
    }

    private BundleSnapshot Snapshot(string id, string directory)
    {
        var enabled = !_bundles.TryGetEnabled(id, out var stored) || stored;
        var skills = new List<BundleSkillSnapshot>();
        var skillsDir = Path.Combine(directory, "skills");
        if (Directory.Exists(skillsDir))
        {
            foreach (var skill in Directory.EnumerateDirectories(skillsDir).Order(StringComparer.Ordinal))
            {
                var skillId = Path.GetFileName(skill);
                skills.Add(new BundleSkillSnapshot(skillId, _skills.IsEnabled(id, skillId)));
            }
        }

        return new BundleSnapshot(id, enabled, _scope == BundleInstallScope.Project, ["hooks listed, not run"], skills, null);
    }
}
