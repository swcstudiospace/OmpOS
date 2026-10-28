namespace Autonogrammer.Sandbox;

/// <summary>
/// One already-running worker in a fixed pool.
/// </summary>
/// <param name="WorkerId">The unique worker identifier; it becomes the container id.</param>
/// <param name="BaseUri">The absolute http(s) base URI of the worker.</param>
/// <param name="VolumeMountPath">The path where the worker has its volume mounted.</param>
public sealed record StaticPoolWorker(string WorkerId, Uri BaseUri, string VolumeMountPath);
