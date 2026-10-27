namespace OmpOS.Sandbox;

/// <summary>
/// Why a cloud environment operation failed.
/// </summary>
public enum CloudEnvironmentFailure
{
    /// <summary>The provider rejected the credentials.</summary>
    Unauthorized,

    /// <summary>The provider rate-limited the request; see <see cref="CloudEnvironmentException.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>The provider answered with a GraphQL error.</summary>
    GraphQlError,

    /// <summary>The request or a worker probe failed in transit (network error, timeout, server error, unexpected status).</summary>
    Transport,

    /// <summary>The service, container or volume does not exist (or no longer exists).</summary>
    NotFound,

    /// <summary>The worker did not answer its readiness probe within the wake window.</summary>
    WakeTimeout,

    /// <summary>No free worker is left in a fixed pool.</summary>
    CapacityExhausted,

    /// <summary>The provider does not support the operation.</summary>
    Unsupported,

    /// <summary>The provider's answer was malformed or reported a state that must not be adopted.</summary>
    InvalidResponse,
}
