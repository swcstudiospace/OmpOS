using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary><c>GET /hooks</c>: the effective hooks of the server workspace, read-only. No hook process and no engine session is ever started.</summary>
public sealed class WorkspaceHooksEndpointTests
{
    private const string HooksJson = """{ "version": 1, "hooks": { "beforeShellExecution": [{ "command": "./hooks/audit.sh" }] } }""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A host with no workspace folder reports the hooks as unavailable with a reason.</summary>
    [Fact]
    public async Task WithoutAWorkspaceFolder_IsUnavailable()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/hooks", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("Api:Workspace:Root", Case.Sensitive);
        document.RootElement.GetProperty("hooks").GetArrayLength().ShouldBe(0);
    }

    /// <summary>A project hook file is parsed into hooks with their scope, event, command, flags and a workspace-relative source; no server path appears.</summary>
    [Fact]
    public async Task AProjectHookFile_IsListed_WithoutServerPaths()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(".cursor/hooks.json", HooksJson);
        _ = workspace.Write(".cursor/hooks/audit.sh", "#!/bin/sh\nexit 0\n");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/hooks", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var root = document.RootElement;
        root.GetProperty("available").GetBoolean().ShouldBeTrue();
        root.GetProperty("consentRecorded").GetBoolean().ShouldBeFalse();
        var hook = root.GetProperty("hooks").EnumerateArray().ShouldHaveSingleItem();
        hook.GetProperty("eventName").GetString().ShouldBe("beforeShellExecution");
        hook.GetProperty("scope").GetString().ShouldBe("project");
        hook.GetProperty("type").GetString().ShouldBe("command");
        hook.GetProperty("command").GetString().ShouldBe("./hooks/audit.sh");
        hook.GetProperty("sourceFile").GetString().ShouldBe(".cursor/hooks.json");
        hook.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        hook.GetProperty("blocking").GetBoolean().ShouldBeTrue();
        hook.GetProperty("failClosed").GetBoolean().ShouldBeFalse();
        hook.GetProperty("preConsented").GetBoolean().ShouldBeFalse();
        hook.GetProperty("timeoutSeconds").GetDouble().ShouldBeGreaterThan(0);
        hook.GetProperty("workingDirectory").GetString().ShouldNotStartWith("/", Case.Sensitive);
        root.GetProperty("sources").EnumerateArray().ShouldHaveSingleItem().GetProperty("file").GetString().ShouldBe(".cursor/hooks.json");
        WorkspaceHost.ShouldNotLeakPaths(body, workspace);
    }

    /// <summary>A malformed hook file is a diagnostic with no server path, not an error response.</summary>
    [Fact]
    public async Task AMalformedHookFile_IsADiagnostic()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(".cursor/hooks.json", "{ not json");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/hooks", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = System.Text.Json.JsonDocument.Parse(body);
        document.RootElement.GetProperty("hooks").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("diagnostics").GetArrayLength().ShouldBeGreaterThan(0);
        WorkspaceHost.ShouldNotLeakPaths(body, workspace);
    }

    /// <summary>A hook file that is a link leaving the workspace folder is not read.</summary>
    [Fact]
    public async Task AHookFileLinkedOutsideTheWorkspace_IsNotRead()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        var target = workspace.Write("hooks.json", HooksJson, outside);
        _ = Directory.CreateDirectory(Path.Combine(workspace.Root, ".cursor"));
        if (!workspace.TryLink(".cursor/hooks.json", target))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/hooks", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("hooks").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("sources").GetArrayLength().ShouldBe(0);
    }

    /// <summary>Reading the hooks, and every other settings and workspace route, requests no hook process and no engine session.</summary>
    [Theory]
    [MemberData(nameof(WorkspaceRoutes.All), MemberType = typeof(WorkspaceRoutes))]
    public async Task NoRoute_StartsAHookOrAnEngine(string route)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(".cursor/hooks.json", HooksJson);
        _ = workspace.Write("a.txt", "a");
        HookRunnerSpy? hooks = null;
        HarnessSessionSpy? engines = null;
        using var factory = WorkspaceHost.Dev(workspace, more: services => (hooks, engines) = WorkspaceHost.AddSpies(services));
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, route, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        hooks.ShouldNotBeNull().Calls.ShouldBe(0);
        engines.ShouldNotBeNull().Sessions.ShouldBe(0);
    }
}
