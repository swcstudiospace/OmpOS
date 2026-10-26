namespace OmpOS.Sandbox;

/// <summary>
/// A cloud session volume as reported by <see cref="ICloudEnvironmentClient"/>.
/// </summary>
/// <param name="VolumeId">The provider volume identifier.</param>
/// <param name="SessionId">The session the volume belongs to.</param>
public sealed record CloudVolume(string VolumeId, string SessionId);
