using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Installs the pinned Bun archive for one platform into <c>~/.bun/bin/bun</c>.</summary>
/// <param name="Summary">The confirmed line.</param>
/// <param name="Version">The pinned Bun version.</param>
/// <param name="PlatformKey">The Bun platform key the remote machine matched.</param>
/// <param name="Asset">The archive name and its pinned SHA-256.</param>
internal sealed record BunInstallAction(string Summary, string Version, string PlatformKey, OmpBunAsset Asset) : ProvisionAction(Summary);
