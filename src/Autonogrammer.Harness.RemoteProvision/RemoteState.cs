namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// What one read-only probe found on a remote machine: the kernel and machine names, the home directory, the tools the install needs, the
/// omp and Bun versions (null when the executable is absent), and the plugin entries of the remote bundle record. It is parsed from the probe's
/// answer; an answer that does not parse never becomes a state.
/// </summary>
/// <param name="ObservedAt">When the probe was read, on the service clock. It is the timestamp an unavailable result reports.</param>
/// <param name="KernelName">The output of <c>uname -s</c>, such as <c>Linux</c> or <c>Darwin</c>.</param>
/// <param name="Machine">The output of <c>uname -m</c>, such as <c>x86_64</c> or <c>arm64</c>.</param>
/// <param name="Home">The absolute home directory of the remote user.</param>
/// <param name="Tools">Which tools the install scripts need are on the path.</param>
/// <param name="OmpPath">Where <c>omp</c> resolves on the path, or null when it does not.</param>
/// <param name="OmpVersion">The version <c>omp --version</c> printed, or null when omp is absent.</param>
/// <param name="BunVersion">The version of the Bun under <c>~/.bun/bin/bun</c>, or null when it is absent.</param>
/// <param name="PluginBundlePresent">Whether <c>~/.local/share/autonogrammer/plugins/bundle.json</c> exists.</param>
/// <param name="Plugins">The entries of that bundle record: one per plugin directory, with its commit.</param>
public sealed record RemoteState(
    DateTimeOffset ObservedAt,
    string KernelName,
    string Machine,
    string Home,
    RemoteTools Tools,
    string? OmpPath,
    string? OmpVersion,
    string? BunVersion,
    bool PluginBundlePresent,
    IReadOnlyList<RemotePluginEntry> Plugins);
