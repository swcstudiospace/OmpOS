namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// A request to start a session. When <see cref="NativeReference"/> is set, the agent resumes that omp conversation. A lost
/// session comes back this way, through a create request that carries its stored reference.
/// </summary>
public sealed record HerdrCreateRequest
{
    /// <summary>Creates a create request.</summary>
    /// <param name="id">The identifier for the new session.</param>
    /// <param name="providerId">The provider selection, as a plain provider identifier.</param>
    /// <param name="nativeReference">The omp session reference to resume, or null to start a new conversation.</param>
    public HerdrCreateRequest(HerdrSessionId id, string providerId, NativeSessionReference? nativeReference)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        Id = id;
        ProviderId = providerId;
        NativeReference = nativeReference;
    }

    /// <summary>Gets the identifier for the new session.</summary>
    public HerdrSessionId Id { get; }

    /// <summary>Gets the provider selection, as a plain provider identifier.</summary>
    public string ProviderId { get; }

    /// <summary>Gets the omp session reference to resume, or null to start a new conversation.</summary>
    public NativeSessionReference? NativeReference { get; }
}
