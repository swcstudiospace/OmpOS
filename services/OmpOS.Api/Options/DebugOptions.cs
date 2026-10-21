namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>OmpOS:Debug</c> configuration section: debug-only behavior switches.
/// </summary>
internal sealed class DebugOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "OmpOS:Debug";

    /// <summary>
    /// Gets or sets whether prompt bodies are logged verbatim. Defaults to false, in which case
    /// prompts are logged as <c>&lt;redacted len=N&gt;</c>.
    /// </summary>
    public bool LogPrompts { get; set; }
}
