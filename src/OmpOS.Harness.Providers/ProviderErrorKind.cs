namespace OmpOS.Harness.Providers;

/// <summary>What a failed provider request means for the user.</summary>
public enum ProviderErrorKind
{
    /// <summary>The account, plan or balance has no quota left.</summary>
    QuotaExhausted = 0,

    /// <summary>The provider is throttling requests that arrive too fast. Waiting clears it.</summary>
    RateLimited,

    /// <summary>A coding plan's paid period has ended, so its key no longer works.</summary>
    PlanExpired,

    /// <summary>The provider refused the credential.</summary>
    CredentialRejected,

    /// <summary>The provider failed or is overloaded. The key and quota are not the cause.</summary>
    ServiceUnavailable,

    /// <summary>The failure matches no mapped case. The message carries the HTTP status.</summary>
    Unrecognised,
}
