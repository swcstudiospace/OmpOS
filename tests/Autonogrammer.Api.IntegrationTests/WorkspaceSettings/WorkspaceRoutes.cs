using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>The routes of the settings and workspace endpoints, as theory data. Each address reaches its handler on a host whose workspace folder holds <c>a.txt</c>.</summary>
internal static class WorkspaceRoutes
{
    /// <summary>Gets one theory row per route.</summary>
    public static TheoryData<string> All => new()
    {
        "/workspace/conventions",
        "/settings/runtime",
        "/settings/plugins",
        "/settings/network",
        "/hooks",
        "/automations",
        "/workspace/tree",
        "/workspace/file?path=a.txt",
    };
}
