using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary><c>GET /workspace/tree</c>: one folder at a time, dot folders included, secret names and escaping links left out.</summary>
public sealed class WorkspaceTreeEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The root listing shows files with sizes and folders, in ordinal name order, and keeps dot folders such as <c>.cursor</c>.</summary>
    [Fact]
    public async Task ListsTheRoot_WithDotFolders_InOrdinalOrder()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("b.txt", "12345");
        _ = workspace.Write("a.txt", "1");
        _ = workspace.Write("src/inner.cs", "x");
        _ = workspace.Write(".cursor/rules/r.mdc", "x");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/tree", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("path").GetString().ShouldBe(string.Empty);
        document.RootElement.GetProperty("truncated").GetBoolean().ShouldBeFalse();
        var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        entries.Select(static entry => entry.GetProperty("name").GetString()).ShouldBe([".cursor", "a.txt", "b.txt", "src"]);
        entries.Single(static entry => string.Equals(entry.GetProperty("name").GetString(), "b.txt", StringComparison.Ordinal)).GetProperty("size").GetInt64().ShouldBe(5);
        entries.Single(static entry => string.Equals(entry.GetProperty("name").GetString(), "src", StringComparison.Ordinal)).GetProperty("kind").GetString().ShouldBe("directory");
        entries.Single(static entry => string.Equals(entry.GetProperty("name").GetString(), "a.txt", StringComparison.Ordinal)).GetProperty("kind").GetString().ShouldBe("file");
    }

    /// <summary>A nested folder lists its own entries and reports its workspace-relative path.</summary>
    [Fact]
    public async Task ListsANestedFolder()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("src/app/one.cs", "x");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/tree?path=src/app", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("path").GetString().ShouldBe("src/app");
        document.RootElement.GetProperty("entries").EnumerateArray().ShouldHaveSingleItem().GetProperty("name").GetString().ShouldBe("one.cs");
    }

    /// <summary>Secret names and the <c>.git</c> folder are not listed.</summary>
    [Fact]
    public async Task SecretNames_AreNotListed()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("keep.txt", "x");
        _ = workspace.Write(".env", "s");
        _ = workspace.Write(".env.local", "s");
        _ = workspace.Write("server.pem", "s");
        _ = workspace.Write("id_rsa", "s");
        _ = workspace.Write("credentials.json", "s");
        _ = workspace.Write(".git/config", "s");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/tree", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("entries").EnumerateArray().Select(static entry => entry.GetProperty("name").GetString()).ShouldBe(["keep.txt"]);
    }

    /// <summary>A link that leaves the workspace is left out of the listing; a link that stays inside is listed.</summary>
    [Fact]
    public async Task LinksThatLeaveTheWorkspace_AreNotListed()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        var outsideFile = workspace.Write("o.txt", "outside", outside);
        var inside = workspace.Write("real.txt", "inside");
        if (!workspace.TryLink("escape.txt", outsideFile) || !workspace.TryLink("escape-dir", outside) || !workspace.TryLink("alias.txt", inside))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/tree", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("entries").EnumerateArray().Select(static entry => entry.GetProperty("name").GetString()).ShouldBe(["alias.txt", "real.txt"]);
    }

    /// <summary>A path below a link that leaves the workspace is 422, a <c>..</c> segment is 400, and a file or an absent folder is 404.</summary>
    [Fact]
    public async Task InvalidPaths_AreRefused()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        _ = workspace.Write("x.txt", "x", outside);
        _ = workspace.Write("file.txt", "x");
        var linked = workspace.TryLink("out", outside);
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var dotDot = await WorkspaceHost.GetAsync(client, "/workspace/tree?path=..", Token).ConfigureAwait(true);
        using var file = await WorkspaceHost.GetAsync(client, "/workspace/tree?path=file.txt", Token).ConfigureAwait(true);
        using var absent = await WorkspaceHost.GetAsync(client, "/workspace/tree?path=nothing", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(dotDot, HttpStatusCode.BadRequest, "invalid_workspace_path", Token).ConfigureAwait(true);
        await WorkspaceHost.ShouldBeProblemAsync(file, HttpStatusCode.NotFound, "workspace_path_not_found", Token).ConfigureAwait(true);
        await WorkspaceHost.ShouldBeProblemAsync(absent, HttpStatusCode.NotFound, "workspace_path_not_found", Token).ConfigureAwait(true);
        if (linked)
        {
            using var escaped = await WorkspaceHost.GetAsync(client, "/workspace/tree?path=out", Token).ConfigureAwait(true);
            await WorkspaceHost.ShouldBeProblemAsync(escaped, HttpStatusCode.UnprocessableEntity, "path_outside_workspace", Token).ConfigureAwait(true);
        }
    }

    /// <summary>A folder with more than 2000 entries is cut at 2000 and says so.</summary>
    [Fact]
    public async Task ALargeFolder_IsTruncated()
    {
        using var workspace = new TempWorkspace();
        for (var index = 0; index < 2005; index++)
        {
            _ = workspace.Write($"big/f{index:D4}.txt", string.Empty);
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/tree?path=big", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("entries").GetArrayLength().ShouldBe(2000);
        document.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    /// <summary>A host with no workspace folder answers 404 <c>workspace_not_configured</c>.</summary>
    [Fact]
    public async Task WithoutAWorkspaceFolder_Is404()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/tree", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_not_configured", Token).ConfigureAwait(true);
    }
}
