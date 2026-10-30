namespace Autonogrammer.Sandbox.Abstractions;

/// <summary>
/// Configuration options for sandbox providers (SBOX-05 isolation defaults).
/// </summary>
public sealed class SandboxProviderOptions
{
    /// <summary>
    /// Gets or sets whether host mounts are allowed. Default false.
    /// </summary>
    public bool AllowHostMounts { get; set; }

    /// <summary>
    /// Gets or sets whether host secrets are allowed. Default false.
    /// </summary>
    public bool AllowHostSecrets { get; set; }

    /// <summary>
    /// Gets or sets whether per-sandbox credentials are required. Default true.
    /// </summary>
    public bool PerSandboxCredentials { get; set; } = true;

    /// <summary>
    /// Gets or sets whether outbound network default is deny. Default true.
    /// </summary>
    public bool OutboundNetworkDefaultDeny { get; set; } = true;

    /// <summary>
    /// Gets or sets the default TTL in minutes. Default 30.
    /// </summary>
    public int DefaultTtlMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum TTL in minutes. Default 480 (8 hours).
    /// </summary>
    public int MaxTtlMinutes { get; set; } = 480;
}
