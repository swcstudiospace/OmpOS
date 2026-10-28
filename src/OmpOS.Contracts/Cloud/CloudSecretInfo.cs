namespace OmpOS.Contracts.Cloud;

/// <summary>What the API tells about a stored secret: that it exists and when it was last written, never its value or its length.</summary>
public sealed record CloudSecretInfo
{
    /// <summary>Gets the secret name, which is also the name of the environment variable it becomes in the worker.</summary>
    public required string Name { get; init; }

    /// <summary>Gets when the secret was last written.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
