using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// What a check or a provision needs about one remote environment. The caller passes the last snapshot it holds, so an unreachable machine
/// can be reported with what was known and when it was read.
/// </summary>
/// <param name="EnvironmentId">The stable identifier of the environment profile. Confirmations are kept per environment.</param>
/// <param name="Profile">The ssh profile. Its <see cref="SshProfile.RemoteOmpPath"/> decides whether omp is provisioned here.</param>
/// <param name="PluginBundle">The desktop's plugin bundle record, read from <c>plugins/bundle.json</c> beside the executable.</param>
/// <param name="LastKnown">The last state read from this machine, or null when there is none.</param>
public sealed record RemoteProvisionRequest(string EnvironmentId, SshProfile Profile, IReadOnlyList<PluginBundleRecord> PluginBundle, RemoteState? LastKnown);
