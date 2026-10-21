namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Secret reference (never contains the actual secret value).
/// </summary>
public sealed record SecretRef
{
    /// <summary>Gets the reference key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the provider-specific secret identifier.</summary>
    public required string ProviderSecretId { get; init; }

    /// <summary>Gets the target environment variable name.</summary>
    public required string TargetEnvVar { get; init; }
}
