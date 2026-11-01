using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Native.Settings.Models;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Cowork;

/// <summary>What the composer needs for one Cowork child.</summary>
/// <param name="SessionDirectory">The session directory.</param>
/// <param name="WorkingDirectory">The child working directory.</param>
/// <param name="TrustedFolders">Folders passed as <c>--add-dir</c>.</param>
/// <param name="Boundary">The folder boundary the resolver uses.</param>
/// <param name="IsTainted">Whether the session is tainted.</param>
internal sealed record CoworkEngineRequest(
    string SessionDirectory,
    string WorkingDirectory,
    IReadOnlyList<string> TrustedFolders,
    CoworkPathBoundary Boundary,
    Func<bool> IsTainted)
{
    /// <summary>Gets the permission mode.</summary>
    public Autonogrammer.Cowork.Tools.CoworkPermissionMode Mode { get; init; } = Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual;

    /// <summary>Gets the provider key snapshot source, or null for an empty dictionary.</summary>
    public ProviderKeyEnvironment? ProviderKeys { get; init; }

    /// <summary>Gets the gateway entry, or null when Cowork tools are degraded.</summary>
    public CoworkGatewayEntry? Gateway { get; init; }

    /// <summary>Gets the user catalog, or null when the head has no services.</summary>
    public McpServerCatalog? Catalog { get; init; }

    /// <summary>Gets whether the session is remote or cloud. Those sessions receive no catalog servers.</summary>
    public bool Remote { get; init; }

    /// <summary>Gets the transport factory.</summary>
    public Func<AcpClientOptions, IAcpTransport>? TransportFactory { get; init; }

    /// <summary>Gets the logger factory.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Gets the omp path.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the omp session to resume.</summary>
    public string? ResumeSessionId { get; init; }

    /// <summary>Gets the standing context paragraph.</summary>
    public string? StandingContext { get; init; }

    /// <summary>Gets the storage folder.</summary>
    public string? StorageFolder { get; init; }

    /// <summary>Gets whether the folder boundary is filesystem isolated.</summary>
    public bool FilesystemIsolated { get; init; }

    /// <summary>Gets whether Skip was acknowledged.</summary>
    public bool SkipAcknowledged { get; init; }

    /// <summary>Gets the isolation wrapper for this Cowork session, or null when isolation is off or not yet consented.</summary>
    public IProcessLaunchWrapper? ProcessWrapper { get; init; }


    /// <summary>Gets catalog servers supplied by a test, or null to read <see cref="Catalog"/>.</summary>
    public IReadOnlyList<AcpMcpServer>? OfferedServers { get; init; }
}
