using OmpOS.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Mcp;

/// <summary>The MCP page states: loading, empty with an example, load failure, invalid configuration, success and degraded, and what each lists.</summary>
public sealed class McpSettingsViewModelStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThePageIsLoadingUntilTheSourceLoads()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready());
        var model = new McpSettingsViewModel(source);

        model.State.ShouldBe(McpViewState.Loading);
        model.IsLoading.ShouldBeTrue();
        model.Rows.ShouldBeEmpty();
        model.ShowTemplates.ShouldBeFalse();

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Success);
        model.IsLoading.ShouldBeFalse();
        model.ShowList.ShouldBeTrue();
    }

    [Fact]
    public async Task NoServersAndNoErrorsIsTheEmptyStateWithAnExample()
    {
        var model = new McpSettingsViewModel(new ScriptedMcpSource());

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Empty);
        model.IsEmpty.ShouldBeTrue();
        model.Heading.ShouldBe(McpCopy.EmptyHeading);
        model.Summary.ShouldBe(McpCopy.Empty);
        McpCopy.ExampleJson.ShouldContain("${env:DOCS_API_KEY}", Case.Sensitive);
        model.CanAddTemplate.ShouldBeTrue();
    }

    [Fact]
    public async Task AFailedLoadIsTheErrorStateWithTheReason()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready()) { LoadFailure = "The state file is locked." };
        var model = new McpSettingsViewModel(source);

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Error);
        model.IsError.ShouldBeTrue();
        model.LoadFailure.ShouldBe("The state file is locked.");
        model.Summary.ShouldBe("The state file is locked.");
        model.ShowList.ShouldBeFalse();
        model.ShowTemplates.ShouldBeFalse();
    }

    [Fact]
    public async Task ALoadThatFailsThenSucceedsRecovers()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready()) { LoadFailure = "locked" };
        var model = new McpSettingsViewModel(source);
        await model.LoadAsync(Token).ConfigureAwait(true);

        source.LoadFailure = null;
        await model.ReloadCommand.ExecuteAsync().ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Success);
        model.LoadFailure.ShouldBeEmpty();
    }

    [Fact]
    public async Task ErrorsAndNoUsableServerIsTheInvalidStateThatListsTheProblems()
    {
        var source = new ScriptedMcpSource
        {
            Notices = [new McpNotice(McpNoticeSeverity.Info, ".mcp.json", null, "No servers."), new McpNotice(McpNoticeSeverity.Error, ".cursor/mcp.json", null, "The file is not valid JSON (line 2, column 4).")],
            Files = [new McpFileEntry(".cursor/mcp.json", McpEntryScope.Project, 0, "The file is not valid JSON (line 2, column 4).")],
        };
        var model = new McpSettingsViewModel(source);

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Invalid);
        model.ShowNotices.ShouldBeTrue();
        model.Notices.Select(notice => notice.SeverityText).ShouldBe(["Error", "Note"]);
        model.Notices[0].Text.ShouldBe(".cursor/mcp.json: The file is not valid JSON (line 2, column 4).");
        model.Groups.Single().HasProblem.ShouldBeTrue();
    }

    [Fact]
    public async Task AnErrorNextToAUsableServerIsDegradedAndAServerNeedingConsentIsDegradedToo()
    {
        var withError = new ScriptedMcpSource(McpEntries.Ready())
        {
            Notices = [new McpNotice(McpNoticeSeverity.Error, ".mcp.json", "bad", "The entry is not an object and is ignored.")],
        };
        var withConsent = new ScriptedMcpSource(McpEntries.Ready(), McpEntries.NeedsConsent());

        var first = new McpSettingsViewModel(withError);
        var second = new McpSettingsViewModel(withConsent);
        await first.LoadAsync(Token).ConfigureAwait(true);
        await second.LoadAsync(Token).ConfigureAwait(true);

        first.State.ShouldBe(McpViewState.Degraded);
        first.Notices.Single().Text.ShouldBe(".mcp.json, server bad: The entry is not an object and is ignored.");
        second.State.ShouldBe(McpViewState.Degraded);
        second.AttentionCount.ShouldBe(1);
        second.Heading.ShouldBe(McpCopy.DegradedHeading);
        second.Summary.ShouldBe("2 servers. 1 need attention and are not offered.");
    }

    [Fact]
    public async Task ServersAreGroupedByFileWithTheirScopeAndTheStatusWordAndDetail()
    {
        var source = new ScriptedMcpSource(McpEntries.NeedsConsent(), McpEntries.Failed(), McpEntries.Ready(), McpEntries.AuthRequired());
        var model = new McpSettingsViewModel(source);

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.Groups.Select(group => group.Heading).ShouldBe(["Project: .cursor/mcp.json", "User: ~/.cursor/mcp.json"]);
        model.Groups[0].CountText.ShouldBe("2 servers");
        model.Groups[0].Rows.Select(row => row.Name).ShouldBe(["tool", "search"]);
        var consent = model.Rows.Single(row => string.Equals(row.Name, "tool", StringComparison.Ordinal));
        consent.StatusText.ShouldBe("Needs approval");
        consent.StatusDetail.ShouldBe("This project server is not offered to sessions until you approve it.");
        consent.CommandText.ShouldBe("node ./tool.js --serve");
        consent.EnvironmentText.ShouldBe("Environment: TOOL_TOKEN");
        consent.NeedsAttention.ShouldBeTrue();
        consent.ShowReview.ShouldBeTrue();
        var failed = model.Rows.Single(row => string.Equals(row.Name, "search", StringComparison.Ordinal));
        failed.StatusDetail.ShouldBe("Not set: environment variable SEARCH_TOKEN.");
        failed.HeadersText.ShouldBe("Headers: Authorization");
        failed.ShowCheck.ShouldBeFalse();
        var auth = model.Rows.Single(row => string.Equals(row.Name, "notes", StringComparison.Ordinal));
        auth.StatusText.ShouldBe("Sign-in needed");
        auth.StatusDetail.ShouldBe("Sign-in needed. Run /mcp reauth notes in the session; omp opens the browser on the machine it runs on.");
        auth.OAuthText.ShouldBe(McpCopy.OAuthNote);
        model.Rows.Single(row => string.Equals(row.Name, "files", StringComparison.Ordinal)).ShowCheck.ShouldBeTrue();
    }

    [Fact]
    public async Task EveryButtonOfEveryRowHasADistinctAccessibleNameThatNamesTheServer()
    {
        var source = new ScriptedMcpSource(McpEntries.NeedsConsent("a"), McpEntries.Ready("b"), McpEntries.Ready("c"));
        var model = new McpSettingsViewModel(source);

        await model.LoadAsync(Token).ConfigureAwait(true);

        var names = model.Rows.SelectMany(row => new[] { row.ToggleName, row.ReviewName, row.RevokeName, row.CheckName, row.OpenName }).ToList();
        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Count);
        model.Rows.ShouldAllBe(row => row.ToggleName.EndsWith(row.Name, StringComparison.Ordinal) && row.CheckName.EndsWith(row.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithoutAWorkspaceNoTemplateCanBeAdded()
    {
        var source = new ScriptedMcpSource { OwnedOrigin = null };
        var model = new McpSettingsViewModel(source);

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.CanAddTemplate.ShouldBeFalse();
        model.TemplatesNeedWorkspace.ShouldBeTrue();
        model.OwnedConfigOrigin.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangedAndPropertyChangedAreRaisedAfterALoad()
    {
        var model = new McpSettingsViewModel(new ScriptedMcpSource(McpEntries.Ready()));
        var changed = 0;
        var properties = new List<string?>();
        model.Changed += (_, _) => changed++;
        model.PropertyChanged += (_, args) => properties.Add(args.PropertyName);

        await model.LoadAsync(Token).ConfigureAwait(true);

        changed.ShouldBeGreaterThanOrEqualTo(2);
        properties.ShouldContain(nameof(McpSettingsViewModel.State), StringComparer.Ordinal);
        properties.ShouldContain(nameof(McpSettingsViewModel.Rows), StringComparer.Ordinal);
        properties.ShouldContain(nameof(McpSettingsViewModel.ShowList), StringComparer.Ordinal);
    }
}
