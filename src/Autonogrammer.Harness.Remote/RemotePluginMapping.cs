namespace Autonogrammer.Harness.Remote;

/// <summary>
/// Says where a local plugin directory has been installed on the remote machine, so an omp argument that points into the local directory
/// can be rewritten to point into the remote one. Autonogrammer does not copy plugins to the remote machine.
/// </summary>
/// <param name="LocalRoot">The absolute directory on this machine that holds the plugin files.</param>
/// <param name="RemoteRoot">The absolute POSIX directory on the remote machine that holds the same files.</param>
public sealed record RemotePluginMapping(string LocalRoot, string RemoteRoot);
