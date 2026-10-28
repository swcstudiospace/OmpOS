using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>Add-from-template: the skeleton goes to the OmpOS-owned <c>.ompos/mcp.json</c> only, keeps what is already there, and loads back as a server.</summary>
public sealed class McpOwnedConfigWriterTests
{
    private static readonly string OwnedPath = McpLocations.OwnedProjectFile(McpTestContext.Root);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(McpTemplateKind.Stdio, McpTransportKind.Stdio)]
    [InlineData(McpTemplateKind.Http, McpTransportKind.Http)]
    public async Task ANewFileIsCreatedAndTheSkeletonLoadsBackAsThatTransport(McpTemplateKind kind, McpTransportKind transport)
    {
        var files = new MemoryMcpFileStore();

        await new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, kind, "starter", Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        files.Writes.ShouldBe([OwnedPath]);
        var starter = set.Servers.Single();
        starter.Name.ShouldBe("starter");
        starter.Transport.ShouldBe(transport);
        starter.Format.ShouldBe(McpConfigFormat.OmpOs);
        files.Text(OwnedPath).ShouldEndWith("\n", Case.Sensitive);
    }

    [Fact]
    public async Task TheHttpSkeletonReadsATokenFromAnEnvironmentVariableThatIsNotSetYet()
    {
        var files = new MemoryMcpFileStore();

        await new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, McpTemplateKind.Http, "remote", Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        var remote = set.Servers.Single();
        remote.UnresolvedVariables.ShouldBe(["env:MCP_EXAMPLE_TOKEN"]);
        remote.IsComplete.ShouldBeFalse();
        remote.HeaderNames.ShouldBe(["Authorization"]);
    }

    [Fact]
    public async Task ExistingServersAndOtherKeysAreKeptAndCommentsAreDropped()
    {
        var files = new MemoryMcpFileStore().With(OwnedPath, """
            // my servers
            { "note": "keep me", "mcpServers": { "mine": { "command": "mine-mcp", "args": ["--x"] } }, "other": [1, 2] }
            """);

        await new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, "added", Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Select(server => server.Name).ShouldBe(["mine", "added"]);
        set.Servers[0].Args.ShouldBe(["--x"]);
        files.Text(OwnedPath).ShouldContain("\"note\": \"keep me\"", Case.Sensitive);
        files.Text(OwnedPath).ShouldContain("\"other\"", Case.Sensitive);
        files.Text(OwnedPath).ShouldNotContain("my servers", Case.Sensitive);
    }

    [Fact]
    public async Task AFileWithoutAServersObjectGetsOne()
    {
        var files = new MemoryMcpFileStore().With(OwnedPath, """{ "note": 1 }""");

        await new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, "added", Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Single().Name.ShouldBe("added");
    }

    [Theory]
    [InlineData("""{ "mcpServers": { "taken": { "command": "x" } } }""", "taken", "already in")]
    [InlineData("{ \"mcpServers\": ", "fresh", "not valid JSON")]
    [InlineData("[]", "fresh", "does not hold a JSON object")]
    [InlineData("""{ "mcpServers": [] }""", "fresh", "not an object")]
    [InlineData("{}", "bad name", "1 to 64 letters")]
    [InlineData("{}", "", "1 to 64 letters")]
    public async Task AnUnusableFileOrNameIsRefusedAndNothingIsWritten(string existing, string name, string expected)
    {
        var files = new MemoryMcpFileStore().With(OwnedPath, existing);

        var failure = await Should.ThrowAsync<McpCatalogException>(
            () => new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, name, Token)).ConfigureAwait(true);

        failure.Message.ShouldContain(expected, Case.Sensitive);
        files.Writes.ShouldBeEmpty();
        files.Text(OwnedPath).ShouldBe(existing);
    }

    [Fact]
    public async Task AnOversizeOrUnreadableFileIsRefused()
    {
        var oversize = new MemoryMcpFileStore().WithBytes(OwnedPath, new byte[McpConfigLimits.MaxFileBytes + 1]);
        var unreadable = new MemoryMcpFileStore().WithUnreadable(OwnedPath);

        var tooLarge = await Should.ThrowAsync<McpCatalogException>(
            () => new McpOwnedConfigWriter(oversize).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, "x", Token)).ConfigureAwait(true);
        var cannotRead = await Should.ThrowAsync<McpCatalogException>(
            () => new McpOwnedConfigWriter(unreadable).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, "x", Token)).ConfigureAwait(true);

        tooLarge.Message.ShouldContain("too large", Case.Sensitive);
        cannotRead.Message.ShouldContain("could not be read", Case.Sensitive);
    }

    [Fact]
    public async Task AWriteFailureBecomesACatalogException()
    {
        var files = new MemoryMcpFileStore { WriteFailure = new IOException("disk full") };

        var failure = await Should.ThrowAsync<McpCatalogException>(
            () => new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, "x", Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("could not be written", Case.Sensitive);
    }

    [Fact]
    public async Task OnlyTheOwnedFileIsEverWritten()
    {
        var other = McpTestContext.Project(".cursor/mcp.json");
        var files = new MemoryMcpFileStore().With(other, """{ "mcpServers": { "a": { "command": "a" } } }""");

        await new McpOwnedConfigWriter(files).AddTemplateAsync(OwnedPath, McpTemplateKind.Stdio, "b", Token).ConfigureAwait(true);

        files.Writes.ShouldBe([OwnedPath]);
        files.Text(other).ShouldBe("""{ "mcpServers": { "a": { "command": "a" } } }""");
    }
}
