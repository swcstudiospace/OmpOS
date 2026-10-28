using System.Net;
using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary><c>GET /workspace/file</c>: text only, 1 MiB cap, containment inside the workspace folder, and secret files that are never shown.</summary>
public sealed class WorkspaceFileEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A text file comes back with its workspace-relative path, size and content.</summary>
    [Fact]
    public async Task ReadsATextFile()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("src/app/readme.txt", "héllo\nworld\n");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=src/app/readme.txt", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("path").GetString().ShouldBe("src/app/readme.txt");
        document.RootElement.GetProperty("content").GetString().ShouldBe("héllo\nworld\n");
        document.RootElement.GetProperty("size").GetInt64().ShouldBe(Encoding.UTF8.GetByteCount("héllo\nworld\n"));
    }

    /// <summary>A byte-order mark is not part of the returned text.</summary>
    [Fact]
    public async Task AByteOrderMark_IsNotReturned()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.WriteBytes("bom.txt", [0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i']);
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=bom.txt", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("content").GetString().ShouldBe("hi");
    }

    /// <summary>A file of exactly 1 MiB is returned; one byte more is 413 <c>file_too_large</c>.</summary>
    [Fact]
    public async Task TheSizeCap_IsOneMebibyte()
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("exact.txt", new string('a', 1024 * 1024));
        _ = workspace.Write("over.txt", new string('a', (1024 * 1024) + 1));
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var exact = await WorkspaceHost.GetAsync(client, "/workspace/file?path=exact.txt", Token).ConfigureAwait(true);
        using var over = await WorkspaceHost.GetAsync(client, "/workspace/file?path=over.txt", Token).ConfigureAwait(true);

        exact.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WorkspaceHost.ShouldBeProblemAsync(over, HttpStatusCode.RequestEntityTooLarge, "file_too_large", Token).ConfigureAwait(true);
    }

    /// <summary>A file with a NUL byte, or bytes that are not UTF-8, is refused as binary (415).</summary>
    [Theory]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x00, 0x04 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0xFD, 0x80 })]
    public async Task ABinaryFile_IsRefused(byte[] content)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.WriteBytes("data.bin", content);
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=data.bin", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "binary_file", Token).ConfigureAwait(true);
    }

    /// <summary>Secret files are answered as absent, whatever folder holds them and whether they are asked for directly or through a link.</summary>
    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("config/.env.production")]
    [InlineData("certs/server.pem")]
    [InlineData("certs/tls.key")]
    [InlineData("home/id_rsa")]
    [InlineData("home/id_ed25519.pub")]
    [InlineData("aws/credentials.json")]
    [InlineData(".git/config")]
    [InlineData(".npmrc")]
    public async Task SecretFiles_AreNeverShown(string secret)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write(secret, "super-secret-value\n");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=" + Uri.EscapeDataString(secret), Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_path_not_found", Token).ConfigureAwait(true);
    }

    /// <summary>An innocent-looking link to a secret file inside the workspace does not show the secret.</summary>
    [Fact]
    public async Task ALinkToASecretFile_IsNeverShown()
    {
        using var workspace = new TempWorkspace();
        var secret = workspace.Write(".env", "super-secret-value\n");
        if (!workspace.TryLink("notes.txt", secret))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=notes.txt", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_path_not_found", Token).ConfigureAwait(true);
    }

    /// <summary>A <c>..</c> segment, a leading separator, a backslash and a missing path are 400.</summary>
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("a/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\b.txt")]
    [InlineData("")]
    public async Task AnInvalidPath_Is400(string path)
    {
        using var workspace = new TempWorkspace();
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=" + Uri.EscapeDataString(path), Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_workspace_path", Token).ConfigureAwait(true);
    }

    /// <summary>A file link that points outside the workspace folder is 422 and its text is never returned.</summary>
    [Fact]
    public async Task AFileLinkThatLeavesTheWorkspace_Is422()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        var target = workspace.Write("outside.txt", "outside-text\n", outside);
        if (!workspace.TryLink("leak.txt", target))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=leak.txt", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        body.ShouldNotContain("outside-text", Case.Sensitive);
        body.ShouldContain("/problems/path_outside_workspace", Case.Sensitive);
    }

    /// <summary>A path below a folder link that points outside the workspace folder is 422.</summary>
    [Fact]
    public async Task APathBelowAFolderLinkThatLeavesTheWorkspace_Is422()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        _ = workspace.Write("deep/outside.txt", "outside-text\n", outside);
        if (!workspace.TryLink("link", outside))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=link/deep/outside.txt", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "path_outside_workspace", Token).ConfigureAwait(true);
    }

    /// <summary>A link that stays inside the workspace folder is followed.</summary>
    [Fact]
    public async Task ALinkInsideTheWorkspace_IsFollowed()
    {
        using var workspace = new TempWorkspace();
        var target = workspace.Write("real/doc.txt", "inside\n");
        if (!workspace.TryLink("alias.txt", target))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=alias.txt", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("content").GetString().ShouldBe("inside\n");
    }

    /// <summary>A file that does not exist, and a folder asked for as a file, are 404.</summary>
    [Theory]
    [InlineData("missing.txt")]
    [InlineData("folder")]
    public async Task AMissingFileOrAFolder_Is404(string path)
    {
        using var workspace = new TempWorkspace();
        _ = workspace.Write("folder/inner.txt", "x");
        using var factory = WorkspaceHost.Dev(workspace);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=" + path, Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_path_not_found", Token).ConfigureAwait(true);
    }

    /// <summary>A host with no workspace folder answers 404 <c>workspace_not_configured</c>.</summary>
    [Fact]
    public async Task WithoutAWorkspaceFolder_Is404()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/workspace/file?path=a.txt", Token).ConfigureAwait(true);

        await WorkspaceHost.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "workspace_not_configured", Token).ConfigureAwait(true);
    }
}
