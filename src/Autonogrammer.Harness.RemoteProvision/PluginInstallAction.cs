namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Clones one plugin repository, checks out the pinned commit, and replaces its directory and bundle entry.</summary>
/// <param name="Summary">The confirmed line.</param>
/// <param name="Record">The bundle entry the install makes true: directory, source and commit.</param>
internal sealed record PluginInstallAction(string Summary, PluginBundleRecord Record) : ProvisionAction(Summary);
