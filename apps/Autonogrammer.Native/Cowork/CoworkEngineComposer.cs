using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Safety;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Settings.Models;

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

    private static Dictionary<string, string> ProviderKeys(ProviderKeyEnvironment? keys)
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
