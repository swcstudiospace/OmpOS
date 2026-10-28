using OmpOS.Harness.Acp;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests.Config;

/// <summary>The registry facade: states, consent gating, the user's switches, connection checks (with a fake checker), add-from-template and the offered ACP array.</summary>
public sealed class McpServerCatalogTests
{
    private const string Canary = "CANARY-77be";

    private static readonly string CursorProject = McpTestContext.Project(".cursor/mcp.json");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static McpServerCatalog Create(MemoryMcpFileStore files, InMemoryMcpStateStore? state = null, IMcpConnectionChecker? checker = null) =>
        new(files, state ?? new InMemoryMcpStateStore(), checker);

    private static async Task<McpServerCatalog> LoadedAsync(MemoryMcpFileStore files, InMemoryMcpStateStore? state = null, IMcpConnectionChecker? checker = null, params (string, string?)[] environment)
    {
        var catalog = Create(files, state, checker);
        _ = await catalog.RefreshAsync(McpTestContext.Create(environment), Token).ConfigureAwait(true);
        return catalog;
    }

    private static McpCatalogEntry Entry(McpServerCatalog catalog, string name) =>
        catalog.Current.Entries.Single(entry => string.Equals(entry.Server.Name, name, StringComparison.Ordinal));

    private static string Project(string command) => "{ \"mcpServers\": { \"s\": { \"command\": \"" + command + "\" } } }";

    [Fact]
    public async Task ABrandNewCatalogIsEmptyBeforeTheFirstRefresh()
    {
        var catalog = Create(new MemoryMcpFileStore());

        catalog.Current.ShouldBeSameAs(McpCatalogSnapshot.Empty);
        catalog.OfferedServers().ShouldBeEmpty();
        await Should.ThrowAsync<McpCatalogException>(() => catalog.SetEnabledAsync("none", true, Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task AUserServerIsConfiguredAndOfferedWithoutConsent()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), Project("user-tool"));

        var catalog = await LoadedAsync(files).ConfigureAwait(true);

        var entry = Entry(catalog, "s");
        entry.Status.State.ShouldBe(McpServerState.Configured);
        entry.IsOffered.ShouldBeTrue();
        catalog.OfferedServers().Single().Name.ShouldBe("s");
    }

    [Fact]
    public async Task AProjectStdioServerNeedsConsentUntilApprovedAndIsNotOfferedUntilThen()
    {
        var files = new MemoryMcpFileStore().With(CursorProject, Project("project-tool"));
        var state = new InMemoryMcpStateStore();
        var catalog = await LoadedAsync(files, state).ConfigureAwait(true);

        Entry(catalog, "s").Status.State.ShouldBe(McpServerState.NeedsConsent);
        Entry(catalog, "s").IsOffered.ShouldBeFalse();
        catalog.OfferedServers().ShouldBeEmpty();

        await catalog.GrantConsentAsync("s", Token).ConfigureAwait(true);

        Entry(catalog, "s").Status.State.ShouldBe(McpServerState.Configured);
        Entry(catalog, "s").IsConsentGranted.ShouldBeTrue();
        catalog.OfferedServers().Single().ShouldBeOfType<AcpStdioMcpServer>().Command.ShouldBe("project-tool");
        state.State.ConsentedIdentities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ChangingTheDeclarationAfterApprovalAsksForConsentAgain()
    {
        var files = new MemoryMcpFileStore().With(CursorProject, Project("project-tool"));
        var state = new InMemoryMcpStateStore();
        var catalog = await LoadedAsync(files, state).ConfigureAwait(true);
        await catalog.GrantConsentAsync("s", Token).ConfigureAwait(true);

        _ = files.With(CursorProject, Project("project-tool --evil"));
        _ = await catalog.RefreshAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        Entry(catalog, "s").Status.State.ShouldBe(McpServerState.NeedsConsent);
        catalog.OfferedServers().ShouldBeEmpty();
    }

    [Fact]
    public async Task ApprovalPersistsAcrossACatalogRestartAndCanBeWithdrawn()
    {
        var files = new MemoryMcpFileStore().With(CursorProject, Project("project-tool"));
        var state = new InMemoryMcpStateStore();
        var first = await LoadedAsync(files, state).ConfigureAwait(true);
        await first.GrantConsentAsync("s", Token).ConfigureAwait(true);

        var second = await LoadedAsync(files, state).ConfigureAwait(true);
        Entry(second, "s").IsOffered.ShouldBeTrue();

        await second.RevokeConsentAsync("s", Token).ConfigureAwait(true);
        Entry(second, "s").Status.State.ShouldBe(McpServerState.NeedsConsent);
    }

    [Fact]
    public async Task AProjectRemoteServerThatReadsTheEnvironmentNeedsConsentAndOneThatDoesNotIsOffered()
    {
        var files = new MemoryMcpFileStore().With(CursorProject, """
            { "mcpServers": {
                "leaky": { "url": "https://x.example.com/mcp", "headers": { "Authorization": "Bearer ${env:TOKEN}" } },
                "plain": { "url": "https://y.example.com/mcp" } } }
            """);

        var catalog = await LoadedAsync(files, environment: [("TOKEN", "t")]).ConfigureAwait(true);

        Entry(catalog, "leaky").Status.State.ShouldBe(McpServerState.NeedsConsent);
        Entry(catalog, "plain").IsOffered.ShouldBeTrue();
        catalog.OfferedServers().Select(server => server.Name).ShouldBe(["plain"]);
    }

    [Fact]
    public async Task ADisabledServerIsNotOfferedAndTheUsersChoiceBeatsTheFile()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.User(".cursor/mcp.json"), "{ \"mcpServers\": { \"off\": { \"command\": \"x\", \"disabled\": true }, \"on\": { \"command\": \"y\" } } }");
        var state = new InMemoryMcpStateStore();
        var catalog = await LoadedAsync(files, state).ConfigureAwait(true);

        Entry(catalog, "off").Status.State.ShouldBe(McpServerState.Disabled);
        Entry(catalog, "off").Status.Reason.ShouldBe("Switched off in ~/.cursor/mcp.json.");
        catalog.OfferedServers().Select(server => server.Name).ShouldBe(["on"]);

        await catalog.SetEnabledAsync("off", true, Token).ConfigureAwait(true);
        await catalog.SetEnabledAsync("on", false, Token).ConfigureAwait(true);

        catalog.OfferedServers().Select(server => server.Name).ShouldBe(["off"]);
        Entry(catalog, "on").Status.Reason.ShouldBe("Switched off in OmpOS.");
        state.Saves.ShouldBe(2);
    }

    [Fact]
    public async Task AServerWithAnUnsetVariableFailsWithTheVariableNameAndIsNotOffered()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"),
            """{ "mcpServers": { "s": { "command": "run", "env": { "K": "${env:API_KEY}" }, "args": ["${input:x}"] } } }""");

        var catalog = await LoadedAsync(files).ConfigureAwait(true);

        var status = Entry(catalog, "s").Status;
        status.State.ShouldBe(McpServerState.Failed);
        status.Reason.ShouldBe("Not set: an unsupported input (input:x), environment variable API_KEY.");
        catalog.OfferedServers().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnInvalidConfigurationLeavesOtherServersAndListsTheProblems()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.Project(".mcp.json"), "{ nope")
            .With(McpTestContext.User(".cursor/mcp.json"), Project("fine"));

        var catalog = await LoadedAsync(files).ConfigureAwait(true);

        catalog.Current.Entries.Select(entry => entry.Server.Name).ShouldBe(["s"]);
        catalog.Current.Files.Select(file => file.Problem is null).ShouldBe([false, true]);
        catalog.Current.Diagnostics.Single().Severity.ShouldBe(McpDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ACheckMarksTheServerConnectedWithItsToolCountAndRaisesChanged()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), Project("tool"));
        var checker = new FakeMcpConnectionChecker(_ => new McpConnectionResult(McpConnectionOutcome.Connected, ToolCount: 3));
        var catalog = await LoadedAsync(files, checker: checker).ConfigureAwait(true);
        var seen = new List<McpServerState>();
        catalog.Changed += (_, _) => seen.Add(Entry(catalog, "s").Status.State);

        var status = await catalog.CheckAsync("s", Token).ConfigureAwait(true);

        status.ShouldBe(new McpServerStatus(McpServerState.Connected, null, 3));
        Entry(catalog, "s").Status.ToolCount.ShouldBe(3);
        seen.ShouldBe([McpServerState.Connecting, McpServerState.Connected]);
        checker.Checked.ShouldBe(["s"]);
    }

    [Fact]
    public async Task AFailedCheckKeepsTheReasonWithEverySecretRemovedAndTheServerStaysOffered()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), $$"""
            { "mcpServers": { "s": { "url": "https://h.example.com/mcp?key={{Canary}}-q", "headers": { "Authorization": "Bearer {{Canary}}-h" } },
                              "t": { "command": "run", "env": { "K": "{{Canary}}-e" } } } }
            """);
        var checker = new FakeMcpConnectionChecker(server => new McpConnectionResult(
            McpConnectionOutcome.Failed,
            $"401 for {server.Url} with {string.Join(' ', server.Headers.Values)} {string.Join(' ', server.Env.Values)}\nsecond line"));
        var catalog = await LoadedAsync(files, checker: checker).ConfigureAwait(true);

        var remote = await catalog.CheckAsync("s", Token).ConfigureAwait(true);
        var local = await catalog.CheckAsync("t", Token).ConfigureAwait(true);

        remote.State.ShouldBe(McpServerState.Failed);
        remote.Reason.ShouldNotBeNull().ShouldNotContain("CANARY", Case.Sensitive);
        remote.Reason.ShouldContain("***", Case.Sensitive);
        remote.Reason.ShouldNotContain("\n", Case.Sensitive);
        local.Reason.ShouldNotBeNull().ShouldNotContain("CANARY", Case.Sensitive);
        Entry(catalog, "s").IsOffered.ShouldBeTrue();
    }

    [Fact]
    public async Task AnAuthChallengeMakesTheServerAuthRequiredAndAKeptResultIsForgottenWhenTheDeclarationChanges()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), "{ \"mcpServers\": { \"s\": { \"url\": \"https://h.example.com/mcp\" } } }");
        var checker = new FakeMcpConnectionChecker(_ => new McpConnectionResult(McpConnectionOutcome.AuthRequired));
        var catalog = await LoadedAsync(files, checker: checker).ConfigureAwait(true);

        _ = await catalog.CheckAsync("s", Token).ConfigureAwait(true);
        Entry(catalog, "s").Status.State.ShouldBe(McpServerState.AuthRequired);
        Entry(catalog, "s").IsOffered.ShouldBeTrue();

        _ = files.With(McpTestContext.User(".cursor/mcp.json"), "{ \"mcpServers\": { \"s\": { \"url\": \"https://other.example.com/mcp\" } } }");
        _ = await catalog.RefreshAsync(McpTestContext.Create(), Token).ConfigureAwait(true);

        Entry(catalog, "s").Status.State.ShouldBe(McpServerState.Configured);
    }

    [Fact]
    public async Task OnlyAnOfferedServerIsChecked()
    {
        var files = new MemoryMcpFileStore().With(CursorProject, Project("needs-consent"));
        var checker = new FakeMcpConnectionChecker(_ => new McpConnectionResult(McpConnectionOutcome.Connected));
        var catalog = await LoadedAsync(files, checker: checker).ConfigureAwait(true);

        var status = await catalog.CheckAsync("s", Token).ConfigureAwait(true);

        status.State.ShouldBe(McpServerState.NeedsConsent);
        checker.Checked.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACatalogWithoutACheckerRefusesToCheckAndAThrowingCheckerLeavesNoStaleConnecting()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".cursor/mcp.json"), Project("tool"));
        var without = await LoadedAsync(files).ConfigureAwait(true);
        var broken = new FakeMcpConnectionChecker(_ => new McpConnectionResult(McpConnectionOutcome.Connected)) { Failure = new InvalidOperationException("bug") };
        var withBroken = await LoadedAsync(files, checker: broken).ConfigureAwait(true);

        without.CanCheck.ShouldBeFalse();
        await Should.ThrowAsync<McpCatalogException>(() => without.CheckAsync("s", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => withBroken.CheckAsync("s", Token)).ConfigureAwait(true);
        Entry(withBroken, "s").Status.State.ShouldBe(McpServerState.Configured);
    }

    [Fact]
    public async Task AStateFileThatCannotBeWrittenIsACatalogExceptionAndChangesNothing()
    {
        var files = new MemoryMcpFileStore().With(CursorProject, Project("tool"));
        var state = new InMemoryMcpStateStore { SaveFailure = new IOException("read-only") };
        var catalog = await LoadedAsync(files, state).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<McpCatalogException>(() => catalog.GrantConsentAsync("s", Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("The MCP state file could not be written.");
        Entry(catalog, "s").Status.State.ShouldBe(McpServerState.NeedsConsent);
    }

    [Fact]
    public async Task ATemplateIsWrittenToTheOwnedFileSwitchedOffAndShownAsDisabled()
    {
        var files = new MemoryMcpFileStore();
        var state = new InMemoryMcpStateStore();
        var catalog = await LoadedAsync(files, state).ConfigureAwait(true);

        var origin = await catalog.AddTemplateAsync(McpTemplateKind.Stdio, "starter", Token).ConfigureAwait(true);

        origin.ShouldBe(".ompos/mcp.json");
        files.Writes.ShouldBe([McpLocations.OwnedProjectFile(McpTestContext.Root)]);
        Entry(catalog, "starter").Status.State.ShouldBe(McpServerState.Disabled);
        Entry(catalog, "starter").Server.Origin.ShouldBe(".ompos/mcp.json");
        catalog.OfferedServers().ShouldBeEmpty();
        catalog.Current.OwnedConfigOrigin.ShouldBe(".ompos/mcp.json");
    }

    [Fact]
    public async Task ATemplateNameThatAnotherFileUsesIsRefusedBeforeAnythingIsWritten()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".claude.json"), "{ \"mcpServers\": { \"github\": { \"command\": \"gh-mcp\" } } }");
        var catalog = await LoadedAsync(files).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<McpCatalogException>(() => catalog.AddTemplateAsync(McpTemplateKind.Http, "github", Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("A server named 'github' is already configured in ~/.claude.json.");
        files.Writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ATemplateNeedsAnOpenWorkspace()
    {
        var catalog = Create(new MemoryMcpFileStore());
        _ = await catalog.RefreshAsync(new McpLocatorContext([], McpTestContext.Home, new Dictionary<string, string?>(StringComparer.Ordinal)), Token).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<McpCatalogException>(() => catalog.AddTemplateAsync(McpTemplateKind.Stdio, "x", Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("Open a workspace folder", Case.Sensitive);
        catalog.Current.OwnedConfigOrigin.ShouldBeNull();
    }

    [Fact]
    public async Task APresetIsWrittenToTheFirstRootsOwnedFileSwitchedOffAndNeverOffered()
    {
        var files = new MemoryMcpFileStore();
        var state = new InMemoryMcpStateStore();
        var catalog = await LoadedAsync(files, state).ConfigureAwait(true);

        var origin = await catalog.AddPresetAsync("linear", null, Token).ConfigureAwait(true);

        origin.ShouldBe(".ompos/mcp.json");
        files.Writes.ShouldBe([McpLocations.OwnedProjectFile(McpTestContext.Root)]);
        Entry(catalog, "linear").Status.State.ShouldBe(McpServerState.Disabled);
        Entry(catalog, "linear").IsEnabled.ShouldBeFalse();
        catalog.OfferedServers().ShouldBeEmpty("a preset is not offered to a session until the user switches it on");
        state.State.EnabledOverride("linear").ShouldBe(false);
    }

    [Fact]
    public async Task APresetCanBeAddedUnderAnotherNameAndIsThenFoundUnderThatName()
    {
        var catalog = await LoadedAsync(new MemoryMcpFileStore()).ConfigureAwait(true);

        _ = await catalog.AddPresetAsync("fetch", "web-fetch", Token).ConfigureAwait(true);

        Entry(catalog, "web-fetch").Server.CommandPreview.ShouldContain("mcp-server-fetch", Case.Sensitive);
        catalog.Current.Entries.Select(entry => entry.Server.Name).ShouldBe(["web-fetch"]);
    }

    [Fact]
    public async Task APresetNameThatAnotherFileUsesIsRefusedBeforeAnythingIsWritten()
    {
        var files = new MemoryMcpFileStore().With(McpTestContext.User(".claude.json"), "{ \"mcpServers\": { \"linear\": { \"command\": \"my-linear\" } } }");
        var catalog = await LoadedAsync(files).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<McpCatalogException>(() => catalog.AddPresetAsync("linear", null, Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("A server named 'linear' is already configured in ~/.claude.json.");
        files.Writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnknownPresetAndAMissingWorkspaceAreRefused()
    {
        var catalog = await LoadedAsync(new MemoryMcpFileStore()).ConfigureAwait(true);
        var unknown = await Should.ThrowAsync<McpCatalogException>(() => catalog.AddPresetAsync("nope", null, Token)).ConfigureAwait(true);
        var closed = Create(new MemoryMcpFileStore());
        _ = await closed.RefreshAsync(new McpLocatorContext([], McpTestContext.Home, new Dictionary<string, string?>(StringComparer.Ordinal)), Token).ConfigureAwait(true);

        var noWorkspace = await Should.ThrowAsync<McpCatalogException>(() => closed.AddPresetAsync("linear", null, Token)).ConfigureAwait(true);

        unknown.Message.ShouldBe("There is no MCP preset named 'nope'.");
        noWorkspace.Message.ShouldContain("Open a workspace folder", Case.Sensitive);
    }

    [Fact]
    public async Task OfferedServersKeepPrecedenceOrderAndMapEveryTransport()
    {
        var files = new MemoryMcpFileStore()
            .With(McpTestContext.User(".claude.json"), "{ \"mcpServers\": { \"z-user\": { \"type\": \"sse\", \"url\": \"https://s.example.com/sse\" } } }")
            .With(McpTestContext.Project(".ompos/mcp.json"), "{ \"mcpServers\": { \"a-http\": { \"url\": \"https://a.example.com/mcp\" } } }");

        var catalog = await LoadedAsync(files).ConfigureAwait(true);

        catalog.OfferedServers().Select(server => server.GetType()).ShouldBe([typeof(AcpHttpMcpServer), typeof(AcpSseMcpServer)]);
        catalog.OfferedServers().Select(server => server.Name).ShouldBe(["a-http", "z-user"]);
    }
}
