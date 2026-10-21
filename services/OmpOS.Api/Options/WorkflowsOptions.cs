namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Workflows</c> configuration section. The Temporal worker arrives in Phase 3;
/// in Phase 2 this is only the enablement switch plus an optional endpoint placeholder.
/// </summary>
internal sealed class WorkflowsOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Workflows";

    /// <summary>Gets or sets whether durable workflows are enabled. Defaults to false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the Temporal endpoint. Unset in Phase 2.</summary>
    public string? TemporalEndpoint { get; set; }
}
