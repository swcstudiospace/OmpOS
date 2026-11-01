namespace Autonogrammer.Harness.PluginBundles;

/// <summary>
/// A server descriptor from a bundle. It stays unconfirmed until a later composition step shows the command line and the user confirms it.
/// This type does not start a process.
/// </summary>
/// <param name="Name">The catalog name plugin.server.</param>
/// <param name="Command">The command after the plugin-root token is expanded.</param>
/// <param name="Arguments">The arguments after expansion.</param>
/// <param name="Environment">The environment pairs after expansion.</param>
/// <param name="WorkingDirectory">The working directory after expansion, or <see langword="null"/>.</param>
/// <param name="OriginBundle">The bundle id.</param>
/// <param name="Confirmed">Always <see langword="false"/> from this source.</param>
/// <param name="Block">Always <c>BundleServerNotConfirmed</c>.</param>
public sealed record BundleMcpServerDescriptor(
    string Name,
    string Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<BundleMcpEnvPair> Environment,
    string? WorkingDirectory,
    string OriginBundle,
    bool Confirmed,
    string Block);
