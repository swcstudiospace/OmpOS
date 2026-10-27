namespace OmpOS.AgentWorker.Configuration;

/// <summary>
/// Binds the <c>Worker:Auth</c> configuration section: the public keys that verify the control plane's worker tokens and the limits a
/// token must respect. The worker holds only public keys, so it can verify a token but never mint one.
/// </summary>
internal sealed class WorkerAuthOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Worker:Auth";

    /// <summary>Gets the audience every worker token must be issued for.</summary>
    public const string Audience = "ompos-worker";

    /// <summary>Gets the longest lifetime (<c>exp</c> minus <c>iat</c>) a worker token may have, and the longest <see cref="MaxTokenLifetime"/> that may be configured.</summary>
    public static readonly TimeSpan MaximumTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Gets the largest clock skew that may be configured.</summary>
    public static readonly TimeSpan MaximumClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the issuer a token must carry. Defaults to <c>ompos-api</c>.</summary>
    public string Issuer { get; set; } = "ompos-api";

    /// <summary>
    /// Gets or sets the PEM-encoded public keys (SubjectPublicKeyInfo, ECDSA P-256 for ES256 or RSA of at least 2048 bits for RS256) a token
    /// signature is verified against. At least one is required; several allow key rotation. A private key is refused.
    /// </summary>
    public List<string> PublicKeysPem { get; set; } = [];

    /// <summary>Gets or sets the longest accepted token lifetime. Defaults to, and may not exceed, <see cref="MaximumTokenLifetime"/>.</summary>
    public TimeSpan MaxTokenLifetime { get; set; } = MaximumTokenLifetime;

    /// <summary>Gets or sets the tolerated clock difference between the control plane and the worker. Defaults to 30 seconds.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
}
