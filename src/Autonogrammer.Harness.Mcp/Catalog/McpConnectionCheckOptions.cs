using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>How an explicit connection check launches a stdio server and how long any check may run.</summary>
public sealed class McpConnectionCheckOptions
{
    /// <summary>The timeout used when none is supplied.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long a check may run before it is reported as timed out.</summary>
    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>Gets the environment mode for a stdio server whose origin is not a plugin bundle.</summary>
    public EngineEnvironmentMode EnvironmentMode { get; init; } = EngineEnvironmentMode.Inherit;

    /// <summary>Gets extra variable names, or prefixes ending in <c>*</c>, kept when the chosen mode is allow-list.</summary>
    public IReadOnlyList<string>? EnvironmentAllowList { get; init; }

    /// <summary>Gets the environment mode for a spec whose <c>Origin</c> starts with <c>plugin:</c>.</summary>
    public EngineEnvironmentMode BundleEnvironmentMode { get; init; } = EngineEnvironmentMode.Allowlist;
}
