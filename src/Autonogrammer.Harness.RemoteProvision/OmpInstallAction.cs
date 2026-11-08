using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Installs one omp release: the tarball is verified against its SHA-512, built with the pinned Bun, and linked from <c>~/.local/bin/omp</c>.</summary>
/// <param name="Summary">The confirmed line.</param>
/// <param name="Release">The release the registry names: version, tarball address and SHA-512.</param>
/// <param name="PlatformKey">The Bun platform key the remote machine matched, which the build must run on.</param>
internal sealed record OmpInstallAction(string Summary, OmpRelease Release, string PlatformKey) : ProvisionAction(Summary);
