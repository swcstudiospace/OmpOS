using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Otel</c> configuration section: which telemetry exporter the host enables.
/// </summary>
internal sealed class OtelOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Otel";

    /// <summary>Gets or sets the exporter: <c>none</c>, <c>console</c>, or <c>otlp</c>. Defaults to <c>none</c>.</summary>
    [RegularExpression("(?i)^(none|console|otlp)$", ErrorMessage = "Otel:Exporter must be one of none, console, or otlp.")]
    public string Exporter { get; set; } = "none";
}
