using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Harness</c> configuration section: which harness adapter serves prompts. <c>InMemory</c> is the development
/// echo adapter and the default in <c>appsettings.json</c>, so the build-time OpenAPI generator can start the host with no
/// environment variables set. <c>Acp</c> runs <c>omp acp</c> over stdio and is the only adapter outside Development.
/// </summary>
internal sealed class HarnessOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Harness";

    /// <summary>Gets the in-memory development adapter name.</summary>
    public const string InMemoryAdapter = "InMemory";

    /// <summary>Gets the ACP adapter name: omp in ACP mode over stdio.</summary>
    public const string AcpAdapter = "Acp";

    /// <summary>Gets or sets the adapter name: <c>InMemory</c> or <c>Acp</c>.</summary>
    [Required(ErrorMessage = "Harness:Adapter is required but was not provided. Set Harness:Adapter (supported values: InMemory, Acp).")]
    public string Adapter { get; set; } = string.Empty;

    /// <summary>Gets or sets the ACP settings. Read only when <see cref="Adapter"/> is <c>Acp</c>.</summary>
    public AcpSettings Acp { get; set; } = new();

    /// <summary>Returns whether <paramref name="adapter"/> names a harness adapter this host can serve.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is supported.</returns>
    public static bool IsSupportedAdapter(string? adapter) => IsInMemoryAdapter(adapter) || IsAcpAdapter(adapter);

    /// <summary>Returns whether <paramref name="adapter"/> names the in-memory development adapter.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is <c>InMemory</c>.</returns>
    public static bool IsInMemoryAdapter(string? adapter) => string.Equals(adapter, InMemoryAdapter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether <paramref name="adapter"/> names the ACP adapter.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is <c>Acp</c>.</returns>
    public static bool IsAcpAdapter(string? adapter) => string.Equals(adapter, AcpAdapter, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>Harness:Acp</c> section: how the ACP adapter starts omp.</summary>
    internal sealed class AcpSettings
    {
        /// <summary>Gets or sets the omp executable, started in ACP mode. Defaults to <c>omp</c> resolved from PATH.</summary>
        public string OmpPath { get; set; } = "omp";
    }
}
