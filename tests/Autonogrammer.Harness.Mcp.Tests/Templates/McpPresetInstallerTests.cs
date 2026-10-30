using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Harness.Mcp.Templates;
using Autonogrammer.Harness.Mcp.Tests.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests.Templates;

/// <summary>A preset goes to the Autonogrammer-owned <c>.autonogrammer/mcp.json</c> only, keeps what is there, holds no credential, and loads back as the server it describes.</summary>
public sealed class McpPresetInstallerTests
{
    private static readonly string OwnedPath = McpLocations.OwnedProjectFile(McpTestContext.Root);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static McpPreset Preset(string id) => McpTemplateCatalog.Find(id).ShouldNotBeNull();

    /// <summary>Every default preset is written under its own id and loads back with the transport, address or command it declares.</summary>
    /// <param name="id">The id of a default preset.</param>
    [Theory]
    [MemberData(nameof(McpTemplateCatalogTests.DefaultIds), MemberType = typeof(McpTemplateCatalogTests))]
    public async Task EveryDefaultPresetLoadsBackAsTheServerItDescribes(string id)
    {
        var preset = Preset(id);
        var files = new MemoryMcpFileStore();

        await new McpPresetInstaller(files).AddPresetAsync(OwnedPath, preset, preset.Id, Token).ConfigureAwait(true);
        var environment = preset.TokenVariable is null ? Array.Empty<(string, string?)>() : [(preset.TokenVariable, "value-for-test")];
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(environment), Token).ConfigureAwait(true);

        files.Writes.ShouldBe([OwnedPath]);
        var server = set.Servers.ShouldHaveSingleItem();
        server.Name.ShouldBe(preset.Id);
        server.Format.ShouldBe(McpConfigFormat.Autonogrammer);
        server.UnresolvedVariables.ShouldBeEmpty();
        if (preset.Kind == McpTemplateKind.Http)
        {
            server.Transport.ShouldBe(McpTransportKind.Http);
            server.Url.ShouldBe(new Uri(preset.Address.ShouldNotBeNull(), UriKind.Absolute));
        }
        else
        {
            server.Transport.ShouldBe(McpTransportKind.Stdio);
            server.Command.ShouldBe(preset.Command);
            server.Args.ShouldBe(preset.Args.Select(argument => argument.Replace("${workspaceFolder}", McpTestContext.Root, StringComparison.Ordinal)));
        }
    }

    /// <summary>A token preset writes a reference to the environment variable and never the token: with the variable unset the server is incomplete and names the variable, with it set the header carries the value only at run time.</summary>
    [Fact]
    public async Task ATokenPresetWritesAReferenceNeverTheToken()
    {
        var files = new MemoryMcpFileStore();

        await new McpPresetInstaller(files).AddPresetAsync(OwnedPath, Preset("github"), "github", Token).ConfigureAwait(true);
        var unset = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(("GITHUB_PERSONAL_ACCESS_TOKEN", "ghp_example")), Token).ConfigureAwait(true);

        files.Text(OwnedPath).ShouldContain("Bearer ${env:GITHUB_PERSONAL_ACCESS_TOKEN}", Case.Sensitive);
        files.Text(OwnedPath).ShouldNotContain("ghp_example", Case.Sensitive);
        unset.Servers.Single().IsComplete.ShouldBeFalse();
        unset.Servers.Single().UnresolvedVariables.ShouldBe(["env:GITHUB_PERSONAL_ACCESS_TOKEN"]);
        set.Servers.Single().Headers["Authorization"].ShouldBe("Bearer ghp_example");
    }

    /// <summary>An OAuth preset holds no header at all: the sign-in happens in the browser.</summary>
    [Fact]
    public async Task AnOAuthPresetHasNoHeaders()
    {
        var files = new MemoryMcpFileStore();

        await new McpPresetInstaller(files).AddPresetAsync(OwnedPath, Preset("linear"), "linear", Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Single().HeaderNames.ShouldBeEmpty();
        files.Text(OwnedPath).ShouldNotContain("headers", Case.Sensitive);
    }

    /// <summary>The user may give the server another name, and a preset added to a file keeps the servers and other keys already there.</summary>
    [Fact]
    public async Task ExistingServersAndKeysAreKeptAndTheNameCanBeChosen()
    {
        var files = new MemoryMcpFileStore().With(OwnedPath, """{ "note": "keep me", "mcpServers": { "mine": { "command": "mine-mcp" } } }""");

        await new McpPresetInstaller(files).AddPresetAsync(OwnedPath, Preset("notion"), "notion-work", Token).ConfigureAwait(true);
        var set = await new McpConfigLoader(files).LoadAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        set.Servers.Select(server => server.Name).Order(StringComparer.Ordinal).ShouldBe(["mine", "notion-work"]);
        files.Text(OwnedPath).ShouldContain("\"note\": \"keep me\"", Case.Sensitive);
    }

    /// <summary>A name that is already in the file, an invalid name, a file that is not an object and a file that is not JSON are refused, and the file is left as it was.</summary>
    /// <param name="existing">The file content.</param>
    /// <param name="name">The server name.</param>
    /// <param name="expected">Text the refusal says.</param>
    [Theory]
    [InlineData("""{ "mcpServers": { "linear": { "command": "x" } } }""", "linear", "already in")]
    [InlineData("""{ "mcpServers": {} }""", "has space", "1 to 64")]
    [InlineData("[]", "linear", "does not hold a JSON object")]
    [InlineData("{ not json", "linear", "not valid JSON")]
    [InlineData("""{ "mcpServers": [] }""", "linear", "not an object")]
    public async Task AnUnusableFileOrNameIsRefusedAndNothingIsWritten(string existing, string name, string expected)
    {
        var files = new MemoryMcpFileStore().With(OwnedPath, existing);

        var failure = await Should.ThrowAsync<McpCatalogException>(
            () => new McpPresetInstaller(files).AddPresetAsync(OwnedPath, Preset("linear"), name, Token)).ConfigureAwait(true);

        failure.Message.ShouldContain(expected, Case.Sensitive);
        files.Writes.ShouldBeEmpty();
        files.Text(OwnedPath).ShouldBe(existing);
    }

    /// <summary>An oversize or unreadable file is refused, and a write failure becomes a catalog exception.</summary>
    [Fact]
    public async Task FileProblemsBecomeCatalogExceptions()
    {
        var oversize = new MemoryMcpFileStore().WithBytes(OwnedPath, new byte[McpConfigLimits.MaxFileBytes + 1]);
        var unreadable = new MemoryMcpFileStore().WithUnreadable(OwnedPath);
        var failing = new MemoryMcpFileStore { WriteFailure = new IOException("disk full") };

        var tooLarge = await Should.ThrowAsync<McpCatalogException>(
            () => new McpPresetInstaller(oversize).AddPresetAsync(OwnedPath, Preset("linear"), "linear", Token)).ConfigureAwait(true);
        var cannotRead = await Should.ThrowAsync<McpCatalogException>(
            () => new McpPresetInstaller(unreadable).AddPresetAsync(OwnedPath, Preset("linear"), "linear", Token)).ConfigureAwait(true);
        var cannotWrite = await Should.ThrowAsync<McpCatalogException>(
            () => new McpPresetInstaller(failing).AddPresetAsync(OwnedPath, Preset("linear"), "linear", Token)).ConfigureAwait(true);

        tooLarge.Message.ShouldContain("too large", Case.Sensitive);
        cannotRead.Message.ShouldContain("could not be read", Case.Sensitive);
        cannotWrite.Message.ShouldContain("could not be written", Case.Sensitive);
    }

    /// <summary>A preset that lacks what its kind needs is a programming error and writes nothing.</summary>
    [Fact]
    public async Task AnIncompletePresetIsRejectedBeforeAnythingIsWritten()
    {
        var files = new MemoryMcpFileStore();
        var noUrl = Preset("linear") with { Address = " " };
        var noCommand = Preset("fetch") with { Command = " " };
        var noVariable = Preset("github") with { TokenVariable = " " };
        var installer = new McpPresetInstaller(files);

        _ = await Should.ThrowAsync<ArgumentException>(() => installer.AddPresetAsync(OwnedPath, noUrl, "a", Token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<ArgumentException>(() => installer.AddPresetAsync(OwnedPath, noCommand, "b", Token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<ArgumentException>(() => installer.AddPresetAsync(OwnedPath, noVariable, "c", Token)).ConfigureAwait(true);

        files.Writes.ShouldBeEmpty();
    }

    /// <summary>Only the owned file is ever written.</summary>
    [Fact]
    public async Task OnlyTheOwnedFileIsWritten()
    {
        var other = McpTestContext.Project(".cursor/mcp.json");
        var files = new MemoryMcpFileStore().With(other, """{ "mcpServers": { "a": { "command": "a" } } }""");

        await new McpPresetInstaller(files).AddPresetAsync(OwnedPath, Preset("vercel"), "vercel", Token).ConfigureAwait(true);

        files.Writes.ShouldBe([OwnedPath]);
        files.Text(other).ShouldBe("""{ "mcpServers": { "a": { "command": "a" } } }""");
    }
}
