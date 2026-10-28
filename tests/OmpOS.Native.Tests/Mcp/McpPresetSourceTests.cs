using System.Text.Json;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;
using OmpOS.Harness.Mcp.Templates;
using OmpOS.Native.Views.Mcp;
using OmpOS.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Mcp;

/// <summary>The presets of the MCP page through the real catalog: the list the adapter offers, the file a preset writes, and the refusals. No server is started and no credential is written.</summary>
public sealed class McpPresetSourceTests
{
    private const string Canary = "CANARY-7e4b";

    private static readonly string Root = Path.Combine("/work", "app");
    private static readonly string Home = Path.Combine("/home", "ada");
    private static readonly string OwnedFile = Path.Combine(Root, ".ompos", "mcp.json");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static McpLocatorContext Context() =>
        new(
            [Root],
            Home,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["GITHUB_PERSONAL_ACCESS_TOKEN"] = Canary + "-github",
                ["CONTEXT7_API_KEY"] = Canary + "-context7",
            });

    private static McpCatalogSettingsSource Create(MemoryMcpFiles files) => new(new McpServerCatalog(files, files), Context, null);

    /// <summary>All thirteen verified presets are offered, each with its transport, a vendor link and a sign-in sentence that names a variable and never a value.</summary>
    [Fact]
    public void ThePageOffersAllThirteenVerifiedPresetsWithTheirSignInNote()
    {
        var presets = Create(new MemoryMcpFiles()).Presets;

        presets.Count.ShouldBe(13);
        presets.Select(static preset => preset.Id).ShouldBe(McpTemplateCatalog.Default.Select(static preset => preset.Id));
        presets.Count(static preset => preset.Transport == McpTemplateChoice.Http).ShouldBe(10);
        presets.Count(static preset => preset.Transport == McpTemplateChoice.Stdio).ShouldBe(3);
        presets.ShouldAllBe(static preset => preset.DocumentationAddress.StartsWith("https://", StringComparison.Ordinal));
        presets.ShouldAllBe(static preset => !string.IsNullOrWhiteSpace(preset.DisplayName) && !string.IsNullOrWhiteSpace(preset.Description));
        presets.Single(static preset => string.Equals(preset.Id, "linear", StringComparison.Ordinal)).AuthNote.ShouldBe(McpCopy.AuthOAuth);
        presets.Single(static preset => string.Equals(preset.Id, "github", StringComparison.Ordinal)).AuthNote.ShouldBe(McpCopy.AuthBearerToken("GITHUB_PERSONAL_ACCESS_TOKEN"));
        presets.Single(static preset => string.Equals(preset.Id, "fetch", StringComparison.Ordinal)).AuthNote.ShouldBe(McpCopy.AuthNone);
        presets.Single(static preset => string.Equals(preset.Id, "linear", StringComparison.Ordinal)).Note.ShouldNotBeNull().ShouldContain("readonly", Case.Sensitive);
    }

    /// <summary>A preset is written to the first root's .ompos/mcp.json, listed switched off and never started.</summary>
    [Fact]
    public async Task AddingAPresetWritesTheOwnedFileAndListsTheServerOff()
    {
        var files = new MemoryMcpFiles();
        var source = Create(files);
        await source.LoadAsync(Token).ConfigureAwait(true);

        var origin = await source.AddPresetAsync("linear", Token).ConfigureAwait(true);

        origin.ShouldBe(".ompos/mcp.json");
        using var document = JsonDocument.Parse(files.Text(OwnedFile).ShouldNotBeNull());
        document.RootElement.GetProperty("mcpServers").GetProperty("linear").GetProperty("url").GetString().ShouldBe("https://mcp.linear.app/mcp");
        var entry = source.Snapshot().Servers.Single();
        entry.Name.ShouldBe("linear");
        entry.Status.ShouldBe(McpServerStatusKind.Disabled);
        entry.IsEnabled.ShouldBeFalse();
        entry.IsOffered.ShouldBeFalse("a switched-off server is not offered to a session");
    }

    /// <summary>No preset writes a token: a token preset holds a reference to the variable, and the value that is set in the environment never reaches the file or the page.</summary>
    [Fact]
    public async Task NoPresetWritesACredential()
    {
        foreach (var preset in McpTemplateCatalog.Default)
        {
            var files = new MemoryMcpFiles();
            var source = Create(files);
            await source.LoadAsync(Token).ConfigureAwait(true);

            await source.AddPresetAsync(preset.Id, Token).ConfigureAwait(true);

            var written = files.Text(OwnedFile).ShouldNotBeNull();
            written.ShouldNotContain(Canary, Case.Sensitive, preset.Id);
            if (preset.Auth == McpPresetAuth.BearerToken)
            {
                written.ShouldContain("${env:" + preset.TokenVariable + "}", Case.Sensitive, preset.Id);
            }

            var shown = source.Snapshot();
            string.Join('\n', shown.Servers.SelectMany(static server => server.EnvNames.Concat(server.HeaderNames).Append(server.CommandPreview))).ShouldNotContain(Canary, Case.Sensitive);
        }
    }

    /// <summary>A name that is already configured, in any file, is refused and the owned file is left as it was.</summary>
    [Fact]
    public async Task AServerWithThePresetsNameIsRefused()
    {
        var files = new MemoryMcpFiles()
            .With(Path.Combine(Home, ".claude.json"), "{ \"mcpServers\": { \"linear\": { \"url\": \"https://mine.example.com/mcp\" } } }");
        var source = Create(files);
        await source.LoadAsync(Token).ConfigureAwait(true);

        var refused = await Should.ThrowAsync<McpSourceException>(() => source.AddPresetAsync("linear", Token)).ConfigureAwait(true);

        refused.Message.ShouldBe("A server named 'linear' is already configured in ~/.claude.json.");
        files.Text(OwnedFile).ShouldBeNull();
    }

    /// <summary>Adding the same preset twice refuses the second time and keeps one entry.</summary>
    [Fact]
    public async Task AddingThePresetTwiceKeepsOneEntry()
    {
        var files = new MemoryMcpFiles();
        var source = Create(files);
        await source.LoadAsync(Token).ConfigureAwait(true);
        await source.AddPresetAsync("fetch", Token).ConfigureAwait(true);

        var refused = await Should.ThrowAsync<McpSourceException>(() => source.AddPresetAsync("fetch", Token)).ConfigureAwait(true);

        refused.Message.ShouldBe("A server named 'fetch' is already configured in .ompos/mcp.json.");
        using var document = JsonDocument.Parse(files.Text(OwnedFile).ShouldNotBeNull());
        document.RootElement.GetProperty("mcpServers").EnumerateObject().Select(static property => property.Name).ShouldBe(["fetch"]);
    }

    /// <summary>An unknown preset id is a source exception, and nothing is written.</summary>
    [Fact]
    public async Task AnUnknownPresetIsRefused()
    {
        var files = new MemoryMcpFiles();
        var source = Create(files);
        await source.LoadAsync(Token).ConfigureAwait(true);

        var refused = await Should.ThrowAsync<McpSourceException>(() => source.AddPresetAsync("nope", Token)).ConfigureAwait(true);

        refused.Message.ShouldBe("There is no MCP preset named 'nope'.");
        files.Text(OwnedFile).ShouldBeNull();
    }

    /// <summary>With no workspace open there is no owned file: the preset is refused with the same sentence as a template.</summary>
    [Fact]
    public async Task WithoutAWorkspaceThePresetIsRefused()
    {
        var files = new MemoryMcpFiles();
        var source = new McpCatalogSettingsSource(new McpServerCatalog(files, files), static () => new McpLocatorContext([], Home, new Dictionary<string, string?>(StringComparer.Ordinal)), null);
        await source.LoadAsync(Token).ConfigureAwait(true);

        var refused = await Should.ThrowAsync<McpSourceException>(() => source.AddPresetAsync("linear", Token)).ConfigureAwait(true);

        refused.Message.ShouldContain("Open a workspace folder first", Case.Sensitive);
    }
}
