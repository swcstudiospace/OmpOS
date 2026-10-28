namespace Autonogrammer.Api.Cloud;

/// <summary>A worker as the control plane knows it: its provider identifiers and the labels that tie it to a cloud agent.</summary>
/// <param name="ContainerId">The provider identifier of the worker container.</param>
/// <param name="SessionId">The session the worker was created for.</param>
/// <param name="VolumeId">The provider identifier of its volume, or null.</param>
/// <param name="Labels">The labels the worker carries. <see cref="CloudWorkerLabels.AgentKey"/> names its agent.</param>
internal sealed record CloudWorkerInfo(string ContainerId, string SessionId, string? VolumeId, IReadOnlyDictionary<string, string> Labels);
