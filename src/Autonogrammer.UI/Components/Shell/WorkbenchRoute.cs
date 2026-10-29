using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.UI.Components.Shell;

/// <summary>
/// An address of the workbench shell: the Agents view, the Kanban view, one session, the Settings dialog or the About view. The shell reads it
/// from the browser address, so every view can be opened, shared and reached with Back and Forward.
/// </summary>
/// <param name="Kind">The kind of address.</param>
/// <param name="SessionId">The session of a <see cref="WorkbenchViewKind.Session"/> address, otherwise null.</param>
/// <param name="SettingsPage">The page name of a <see cref="WorkbenchViewKind.Settings"/> address (a settings section such as <c>models</c>), or null for the first page.</param>
public sealed record WorkbenchRoute(WorkbenchViewKind Kind, string? SessionId = null, string? SettingsPage = null)
{
    private const string KanbanSegment = "kanban";
    private const string AboutSegment = "about";
    private const string SessionsSegment = "sessions";
    private const string SettingsSegment = "settings";

    private static readonly char[] QueryOrFragment = ['?', '#'];

    /// <summary>Gets the Agents view.</summary>
    public static WorkbenchRoute Agents { get; } = new(WorkbenchViewKind.Agents);

    /// <summary>Gets the Kanban view.</summary>
    public static WorkbenchRoute Kanban { get; } = new(WorkbenchViewKind.Kanban);

    /// <summary>Gets the About view.</summary>
    public static WorkbenchRoute About { get; } = new(WorkbenchViewKind.About);

    /// <summary>Gets the path of this route relative to the base address, without a leading slash; the home page is an empty string.</summary>
    public string RelativePath => Kind switch
    {
        WorkbenchViewKind.Kanban => KanbanSegment,
        WorkbenchViewKind.About => AboutSegment,
        WorkbenchViewKind.Session when SessionId is { Length: > 0 } id => SessionsSegment + "/" + Uri.EscapeDataString(id),
        WorkbenchViewKind.Settings => SettingsPage is { Length: > 0 } page ? SettingsSegment + "/" + Uri.EscapeDataString(page) : SettingsSegment,
        _ => string.Empty,
    };

    /// <summary>Creates the route of one session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The route.</returns>
    public static WorkbenchRoute ForSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new WorkbenchRoute(WorkbenchViewKind.Session, sessionId);
    }

    /// <summary>Creates the route of the Settings dialog.</summary>
    /// <param name="page">The page name, or null for the first page.</param>
    /// <returns>The route.</returns>
    public static WorkbenchRoute ForSettings(string? page) =>
        new(WorkbenchViewKind.Settings, SettingsPage: string.IsNullOrWhiteSpace(page) ? null : page);

    /// <summary>Reads a path as a workbench route.</summary>
    /// <param name="relativePath">The path relative to the base address, with or without query and fragment; null is the home page.</param>
    /// <param name="route">The route, or null when the path is not one the workbench shows.</param>
    /// <returns>True when the path is the home page, <c>kanban</c>, <c>about</c>, <c>sessions/{id}</c>, <c>settings</c> or <c>settings/{page}</c>.</returns>
    public static bool TryParse(string? relativePath, [NotNullWhen(true)] out WorkbenchRoute? route)
    {
        var path = relativePath ?? string.Empty;
        var cut = path.IndexOfAny(QueryOrFragment);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        route = segments switch
        {
            [] => Agents,
            [var only] when Is(only, KanbanSegment) => Kanban,
            [var only] when Is(only, AboutSegment) => About,
            [var head, var id] when Is(head, SessionsSegment) && Unescape(id) is { Length: > 0 } session => ForSession(session),
            [var head] when Is(head, SettingsSegment) => ForSettings(null),
            [var head, var page] when Is(head, SettingsSegment) => ForSettings(Unescape(page)),
            _ => null,
        };
        return route is not null;
    }

    private static bool Is(string segment, string expected) => string.Equals(segment, expected, StringComparison.OrdinalIgnoreCase);

    private static string Unescape(string segment) => Uri.UnescapeDataString(segment).Trim();
}
