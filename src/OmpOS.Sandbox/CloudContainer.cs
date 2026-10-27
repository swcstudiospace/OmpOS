namespace OmpOS.Sandbox;

/// <summary>
/// A cloud session container as reported by <see cref="ICloudEnvironmentClient"/>.
/// </summary>
/// <param name="ContainerId">The provider container identifier.</param>
/// <param name="SessionId">The session the container belongs to.</param>
/// <param name="Phase">The current lifecycle phase.</param>
/// <param name="BootGeneration">
/// Counts how many times the container's processes have been started (1 for the first boot, incremented by every wake
/// that follows an actual sleep). A different generation means every process that ran earlier is gone.
/// </param>
public sealed record CloudContainer(string ContainerId, string SessionId, CloudContainerPhase Phase, int BootGeneration)
{
    /// <summary>
    /// Gets the http(s) base URI the control plane uses for the worker's health path and, with the scheme switched to ws(s),
    /// its <c>/acp</c> endpoint; <see langword="null"/> for the in-memory fake.
    /// </summary>
    public Uri? WorkerBaseUri { get; init; }
}
