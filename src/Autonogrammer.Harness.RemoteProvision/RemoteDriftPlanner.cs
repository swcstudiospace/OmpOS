using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Compares a probed machine with the desired state and says what differs and what an install would do about it. It is pure: it reads
/// the state and the desired values and writes nothing. An item the remote side cannot provide is reported as blocked, never installed.
/// </summary>
internal static class RemoteDriftPlanner
{
    /// <summary>Plans the installs for one machine.</summary>
    /// <param name="state">The probed state.</param>
    /// <param name="platformKey">The Bun platform key that matches the machine.</param>
    /// <param name="pin">The pinned Bun.</param>
    /// <param name="latestOmp">The latest omp release, or null when this machine's omp is not provisioned here.</param>
    /// <param name="bundle">The desktop's plugin bundle record.</param>
    /// <returns>The drift items and the installs for the actionable ones, in run order.</returns>
    public static ProvisionPlan Plan(RemoteState state, string platformKey, OmpBunPin pin, OmpRelease? latestOmp, IReadOnlyList<PluginBundleRecord> bundle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(bundle);
        var asset = pin.AssetFor(platformKey) ?? throw new InvalidOperationException(RemoteProvisionMessages.NoBunArchive(platformKey));
        var drift = new List<ProvisionDriftItem>();
        var actions = new List<ProvisionAction>();

        var bunDrift = BunDrift(state, pin.Version);
        if (bunDrift is not null)
        {
            drift.Add(bunDrift);
            if (bunDrift.IsActionable)
            {
                actions.Add(new BunInstallAction(RemoteProvisionMessages.BunAction(pin.Version, RemoteProvisionMessages.BunPath(state.Home)), pin.Version, platformKey, asset));
            }
        }

        var bunReady = bunDrift is null || bunDrift.IsActionable;
        if (latestOmp is not null && OmpDrift(state, latestOmp, bunReady, pin.Version) is { } ompDrift)
        {
            drift.Add(ompDrift);
            if (ompDrift.IsActionable)
            {
                actions.Add(new OmpInstallAction(
                    RemoteProvisionMessages.OmpAction(latestOmp.Version, RemoteProvisionMessages.OmpPath(state.Home), pin.Version),
                    latestOmp,
                    platformKey));
            }
        }

        foreach (var record in bundle)
        {
            if (PluginDrift(state, record) is not { } pluginDrift)
            {
                continue;
            }

            drift.Add(pluginDrift);
            if (pluginDrift.IsActionable)
            {
                actions.Add(new PluginInstallAction(
                    RemoteProvisionMessages.PluginAction(record.Dir, record.Commit, record.Source, RemoteProvisionMessages.PluginDirectory(state.Home, record.Dir)),
                    record));
            }
        }

        return new ProvisionPlan(drift, actions);
    }

    private static ProvisionDriftItem? BunDrift(RemoteState state, string pinnedVersion)
    {
        if (string.Equals(state.BunVersion, pinnedVersion, StringComparison.Ordinal))
        {
            return null;
        }

        var blocked = Missing(
            ("bash", state.Tools.Bash),
            ("curl", state.Tools.Curl),
            ("unzip", state.Tools.Unzip),
            ("sha256sum or shasum", state.Tools.Sha256));
        return new ProvisionDriftItem(
            ProvisionComponent.Bun,
            RemoteProvisionMessages.BunPath(state.Home),
            RemoteProvisionMessages.BunDrift(state.BunVersion, pinnedVersion),
            blocked);
    }

    private static ProvisionDriftItem? OmpDrift(RemoteState state, OmpRelease latest, bool bunReady, string pinnedBun)
    {
        if (string.Equals(state.OmpVersion, latest.Version, StringComparison.Ordinal))
        {
            return null;
        }

        var blocked = Missing(
            ("bash", state.Tools.Bash),
            ("curl", state.Tools.Curl),
            ("tar", state.Tools.Tar),
            ("sha512sum or shasum", state.Tools.Sha512));
        blocked ??= bunReady ? null : RemoteProvisionMessages.NeedsBun(pinnedBun);
        return new ProvisionDriftItem(
            ProvisionComponent.Omp,
            RemoteProvisionMessages.OmpPath(state.Home),
            RemoteProvisionMessages.OmpDrift(state.OmpVersion, latest.Version),
            blocked);
    }

    private static ProvisionDriftItem? PluginDrift(RemoteState state, PluginBundleRecord record)
    {
        var remote = state.Plugins.FirstOrDefault(entry => string.Equals(entry.Dir, record.Dir, StringComparison.Ordinal));
        if (remote is not null && string.Equals(remote.Commit, record.Commit, StringComparison.Ordinal))
        {
            return null;
        }

        var blocked = string.Equals(record.Source, RemoteProvisionRules.InRepoSource, StringComparison.Ordinal)
            ? RemoteProvisionMessages.InRepoPlugin(record.Dir)
            : record.NeedsInstall
                ? RemoteProvisionMessages.PluginNeedsInstall(record.Dir)
                : Missing(("bash", state.Tools.Bash), ("git", state.Tools.Git));
        return new ProvisionDriftItem(
            ProvisionComponent.Plugin,
            RemoteProvisionMessages.PluginDirectory(state.Home, record.Dir),
            RemoteProvisionMessages.PluginDrift(remote?.Commit, record.Commit),
            blocked);
    }

    private static string? Missing(params (string Name, bool Present)[] required)
    {
        var missing = required.Where(static tool => !tool.Present).Select(static tool => tool.Name).ToList();
        return missing.Count == 0
            ? null
            : RemoteProvisionMessages.MissingTools(string.Join(", ", required.Select(static tool => tool.Name)), missing);
    }
}
