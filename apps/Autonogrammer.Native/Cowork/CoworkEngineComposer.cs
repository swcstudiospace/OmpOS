using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Tools;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Settings.Models;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Cowork;

/// <summary>Builds a Cowork omp launch and composes it through <see cref="EngineComposition"/>, never the Code planner.</summary>
internal static class CoworkEngineComposer
{
    /// <summary>The notice raised when a catalog server named cowork is dropped because the plan already uses that name.</summary>
    public const string CoworkNameClashNotice = "A catalog server named cowork was not attached. The Cowork gateway keeps that name.";

    private static readonly string[] WindowsExtras =
    [
        "SYSTEMROOT", "SYSTEMDRIVE", "WINDIR", "COMSPEC", "PATHEXT", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP", "PROGRAMDATA", "USERNAME",
    ];

    /// <summary>Composes one Cowork engine. Catalog servers are offered only for a local session.</summary>
    /// <param name="request">The launch inputs.</param>
    /// <returns>The engine and any notices.</returns>
    public static CoworkEngineComposition Compose(CoworkEngineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var keys = ProviderKeys(request.ProviderKeys);
        var gateway = request.Gateway;
        var profile = new CoworkSessionProfile(
            request.SessionDirectory,
            request.WorkingDirectory,
            request.TrustedFolders,
            request.Mode,
            keys,
            gateway?.Url,
            gateway?.Bearer);
        ConfigOverlayWriter.WriteYaml(request.SessionDirectory, CoworkSessionProfile.OverlayYaml);
        var plan = profile.Create(request.StandingContext);
        var notices = new List<string>();
        var extra = CatalogServers(request, notices);
        var options = new EngineCompositionOptions
        {
            ExtraMcpServers = extra,
            EnvironmentMode = EngineEnvironmentMode.Allowlist,
            EnvironmentAllowList = OperatingSystem.IsWindows() ? WindowsExtras : [],
            ProcessWrapper = request.ProcessWrapper,
            PermissionResolverFactory = pending => new CoworkPermissionResolver(
                pending,
                request.Boundary,
                request.TrustedFolders,
                request.StorageFolder,
                request.WorkingDirectory,
                request.Mode,
                request.IsTainted,
                request.FilesystemIsolated,
                request.SkipAcknowledged),
        };
        var engine = EngineComposition.Compose(
            RunModeLaunchMapper.AcpToolKinds,
            plan,
            request.TransportFactory,
            request.LoggerFactory,
            request.OmpPath,
            request.ResumeSessionId,
            options);
        return new CoworkEngineComposition(engine, notices, plan);
    }

    /// <summary>The Windows allow-list extras, named for tests that cannot run on Windows.</summary>
    public static IReadOnlyList<string> WindowsAllowListExtras { get; } = WindowsExtras;

    private static IReadOnlyDictionary<string, string> ProviderKeys(ProviderKeyEnvironment? keys)
    {
        if (keys is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(keys.Current, StringComparer.Ordinal);
    }

    private static IReadOnlyList<AcpMcpServer> CatalogServers(CoworkEngineRequest request, List<string> notices)
    {
        if (request.Remote)
        {
            return [];
        }

        var offered = request.OfferedServers ?? request.Catalog?.OfferedServers() ?? [];
        if (offered.Count == 0 && request.Catalog is null && request.OfferedServers is null)
        {
            return [];
        }
        if (offered.Any(static server => string.Equals(server.Name, "cowork", StringComparison.Ordinal)))
        {
            notices.Add(CoworkNameClashNotice);
        }

        return offered;
    }
}

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


/// <summary>The loopback cowork MCP entry.</summary>
/// <param name="Url">The session endpoint.</param>
/// <param name="Bearer">The bearer. It is placed only in the session header list.</param>
internal sealed record CoworkGatewayEntry(Uri Url, string Bearer);

/// <summary>A composed Cowork engine.</summary>
/// <param name="Engine">The engine.</param>
/// <param name="Notices">Composer notices, including a cowork name clash.</param>
/// <param name="Plan">The launch plan.</param>
internal sealed record CoworkEngineComposition(EngineComposition Engine, IReadOnlyList<string> Notices, OmpLaunchPlan Plan);
