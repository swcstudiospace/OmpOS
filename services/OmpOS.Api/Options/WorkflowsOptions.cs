using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Workflows</c> configuration section. When <see cref="Enabled"/> is true the API
/// starts session runs as Temporal workflows on <see cref="TaskQueue"/> through the client settings
/// <see cref="Address"/>, <see cref="Namespace"/>, <see cref="ApiKey"/> and <see cref="Tls"/>.
/// </summary>
internal sealed class WorkflowsOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Workflows";

    /// <summary>Gets the default Temporal namespace.</summary>
    public const string DefaultNamespace = "default";

    /// <summary>Gets the default task queue; matches the worker's default session-run queue.</summary>
    public const string DefaultTaskQueue = "ompos-session-runs";

    /// <summary>Gets or sets whether durable workflows are enabled. Defaults to false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the Temporal server address (host:port). Required when <see cref="Enabled"/> is true.</summary>
    public string? Address { get; set; }

    /// <summary>Gets or sets the Temporal namespace. Defaults to <c>default</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Namespace { get; set; } = DefaultNamespace;

    /// <summary>Gets or sets the task queue session-run workflows start on. Defaults to <c>ompos-session-runs</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string TaskQueue { get; set; } = DefaultTaskQueue;

    /// <summary>Gets or sets the Temporal API key, sent as a Bearer token. Optional; never logged or echoed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets whether the Temporal client connects over TLS. Defaults to false.</summary>
    public bool Tls { get; set; }
}
