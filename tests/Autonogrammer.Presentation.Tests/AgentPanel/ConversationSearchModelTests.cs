using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>The conversation search overlay model: opening, searching, selecting, accepting, and following the sessions of the rail.</summary>
public sealed class ConversationSearchModelTests
{
    private static ConversationSearchModel Model(out ConversationSearchIndex index)
    {
        index = new ConversationSearchIndex();
        index.Upsert("s1", "Fix login", "the login bug");
        index.Upsert("s2", "Release", "login page redesign");
        return new ConversationSearchModel(index);
    }

    /// <summary>Opening shows the prompt text and no announcement.</summary>
    [Fact]
    public void OpeningShowsThePrompt()
    {
        var model = Model(out _);

        model.Open();

        model.IsOpen.ShouldBeTrue();
        model.EmptyMessage.ShouldBe(ConversationSearchCopy.Prompt);
        model.Announcement.ShouldBeNull();
        model.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>A query lists the matches, selects the first and announces the count; a query nobody matches says so with the query.</summary>
    [Fact]
    public void ASearchListsMatchesOrSaysNone()
    {
        var model = Model(out _);
        model.Open();

        model.SetQuery("login");

        model.Results.Select(static result => result.SessionId).ShouldBe(["s1", "s2"]);
        model.SelectedIndex.ShouldBe(0);
        model.EmptyMessage.ShouldBeNull();
        model.Announcement.ShouldBe("2 conversations");

        model.SetQuery("zebra");

        model.Results.ShouldBeEmpty();
        model.SelectedIndex.ShouldBe(-1);
        model.EmptyMessage.ShouldBe(ConversationSearchCopy.NoMatches("zebra"));
        model.EmptyMessage.ShouldNotBeNull().ShouldContain("zebra", Case.Sensitive);
        model.Announcement.ShouldBe("No results");
    }

    /// <summary>The selection wraps; selecting by index refuses an index that names nothing.</summary>
    [Fact]
    public void TheSelectionWraps()
    {
        var model = Model(out _);
        model.Open();
        model.SetQuery("login");

        model.MoveSelection(1);
        model.SelectedResult!.SessionId.ShouldBe("s2");
        model.MoveSelection(1);
        model.SelectedIndex.ShouldBe(0);
        model.MoveSelection(-1);
        model.SelectedIndex.ShouldBe(1);
        model.Select(0).ShouldBeTrue();
        model.Select(5).ShouldBeFalse();
        model.Select(-1).ShouldBeFalse();
    }

    /// <summary>Accepting yields the selected session and closes the overlay with an empty query; with nothing selected it yields nothing and stays open.</summary>
    [Fact]
    public void AcceptingYieldsTheSessionAndCloses()
    {
        var model = Model(out _);
        model.Open();
        model.Accept().ShouldBeNull();
        model.IsOpen.ShouldBeTrue();
        model.SetQuery("redesign");

        var id = model.Accept();

        id.ShouldBe("s2");
        model.IsOpen.ShouldBeFalse();
        model.Query.ShouldBeEmpty();
        model.Results.ShouldBeEmpty();
    }

    /// <summary>Closing clears the query and results.</summary>
    [Fact]
    public void ClosingClearsTheQuery()
    {
        var model = Model(out _);
        model.Open();
        model.SetQuery("login");

        model.Close();

        model.IsOpen.ShouldBeFalse();
        model.Query.ShouldBeEmpty();
        model.Results.ShouldBeEmpty();
    }

    /// <summary>Syncing with the rail adds titles, removes sessions that are gone, keeps the transcripts, and refreshes the results of an open search.</summary>
    [Fact]
    public void SyncingFollowsTheRail()
    {
        var clock = new ManualTimeProvider();
        var catalog = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        catalog.Sessions.Add(AgentPanelTestData.Summary("s1", SessionLifecycle.Created, now, now, "Fix login"));
        catalog.Sessions.Add(AgentPanelTestData.Summary("s3", SessionLifecycle.Created, now, now, "Deploy docs"));
        using var rail = new SessionCatalogModel(catalog, clock);
        var index = new ConversationSearchIndex();
        index.Upsert("s1", "stale title", "the login bug");
        index.Upsert("s2", "Gone", "login page");
        var model = new ConversationSearchModel(index);
        model.Open();
        model.SetQuery("login");
        model.Results.Count.ShouldBe(2);

        model.SyncSessions(rail.Sessions);

        index.SessionCount.ShouldBe(2);
        model.Results.Select(static result => result.SessionId).ShouldBe(["s1"]);
        model.Results[0].Title.ShouldBe("Fix login");
        model.Results[0].Snippet.ShouldContain("login bug", Case.Sensitive);
        model.SetQuery("deploy");
        model.Results.Single().SessionId.ShouldBe("s3");
    }

    /// <summary>The transcript text holds the user's prompts, the assistant's text, code, errors and tool names, and the streaming tail.</summary>
    [Fact]
    public void TheTranscriptTextCoversTheReadableBlocks()
    {
        var tree = new UiTree
        {
            Blocks =
            [
                new UserMessageBlock { Id = "u1", Text = "please fix the build" },
                new MarkdownBlock { Id = "m1", Markdown = "I will start with the **project file**." },
                new CodeBlock { Id = "k1", Language = "cs", Code = "var x = 1;" },
                new ToolCallBlock { Id = "t1", CallId = "c1", ToolName = "edit_file", Status = "completed", Arguments = JsonDocument.Parse("{}").RootElement.Clone() },
                new ErrorBlock { Id = "e1", Message = "build failed", Retryable = false },
            ],
            StreamingText = "still typing",
        };

        var text = ConversationSearchTranscriptText.From(tree);

        text.ShouldBe("please fix the build\nI will start with the **project file**.\nvar x = 1;\nedit_file\nbuild failed\nstill typing");
        ConversationSearchTranscriptText.From(UiTree.Empty).ShouldBeEmpty();
    }
}
