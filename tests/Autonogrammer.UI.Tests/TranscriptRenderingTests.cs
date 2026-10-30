using System.Text.Json;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Autonogrammer.UI.Components.Blocks;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// The transcript draws only what changed, keeps its blocks keyed, bounds the DOM with "Show earlier messages", and speaks through one polite
/// status region: no live region per code block, no announcement of streamed deltas.
/// </summary>
public sealed class TranscriptRenderingTests : UiTestContext
{
    private const string ScrollModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-scroll.js";
    private const string TranscriptModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-transcript.js";
    private const string ShowEarlier = "button.autonogrammer-show-earlier";
    private const string Status = "[aria-live=\"polite\"]";

    /// <summary>A block that stays while others are inserted before it keeps its component, so its state and DOM are not rebuilt.</summary>
    [Fact]
    public void ABlockKeepsItsComponentWhenAnotherIsInsertedBeforeIt()
    {
        var kept = Markdown("markdown-1", "first answer");
        var cut = RenderSession(Tree(User(0), kept), SessionViewState.Success);
        var before = cut.FindComponents<MarkdownBlockView>().Single().Instance;

        cut.Render(parameters => parameters.Add(p => p.Tree, Tree(User(0), Markdown("markdown-x", "inserted"), kept)));

        var after = cut.FindComponents<MarkdownBlockView>();
        after.Count.ShouldBe(2);
        after.Single(component => string.Equals(component.Instance.Block.Id, "markdown-1", StringComparison.Ordinal)).Instance.ShouldBeSameAs(before);
    }

    /// <summary>Two blocks of one tree can share an identifier (a tool call and the permission asked for it); both are drawn.</summary>
    [Fact]
    public void BlocksWithTheSameIdentifierAreBothDrawn()
    {
        var tree = Tree(User(0), Tool("call-1", "ls", "running"), new PermissionBlock { Id = "call-1", Prompt = "ls", Options = ["allow once"] });

        var cut = RenderSession(tree, SessionViewState.Streaming);

        cut.FindComponents<ToolCallBlockView>().Count.ShouldBe(1);
        cut.FindComponents<PermissionBlockView>().Count.ShouldBe(1);
    }

    /// <summary>A block that changed is drawn again and the blocks around it are not; a streamed text delta draws none of them.</summary>
    [Fact]
    public void ADeltaDrawsOnlyTheBlockThatChanged()
    {
        var user = User(0);
        var answer = Markdown("markdown-1", "an answer");
        var running = Tree(user, answer, Tool("call-1", "ls", "running"));
        var cut = RenderSession(running, SessionViewState.Streaming);
        var userRenders = cut.FindComponent<UserMessageView>().RenderCount;
        var answerRenders = cut.FindComponent<MarkdownBlockView>().RenderCount;
        var toolRenders = cut.FindComponent<ToolCallBlockView>().RenderCount;

        cut.Render(parameters => parameters.Add(p => p.Tree, running with { Blocks = [user, answer, Tool("call-1", "ls", "completed")] }));

        cut.FindComponent<ToolCallBlockView>().RenderCount.ShouldBe(toolRenders + 1);
        cut.FindComponent<MarkdownBlockView>().RenderCount.ShouldBe(answerRenders);
        cut.FindComponent<UserMessageView>().RenderCount.ShouldBe(userRenders);

        var settled = cut.FindComponent<ToolCallBlockView>().RenderCount;
        var streaming = cut.Instance.Tree with { StreamingText = "typing" };
        cut.Render(parameters => parameters.Add(p => p.Tree, streaming));

        cut.Find(".autonogrammer-streaming").TextContent.ShouldBe("typing");
        cut.FindComponent<ToolCallBlockView>().RenderCount.ShouldBe(settled);
        cut.FindComponent<MarkdownBlockView>().RenderCount.ShouldBe(answerRenders);
        cut.FindComponent<UserMessageView>().RenderCount.ShouldBe(userRenders);
    }

    /// <summary>Only the most recent turns are in the DOM at first, and the button says how many are hidden.</summary>
    [Fact]
    public void OnlyTheRecentTurnsAreInTheDomAtFirst()
    {
        var cut = RenderSession(Turns(SessionView.InitialVisibleTurns + 3), SessionViewState.Success);

        cut.FindComponents<UserMessageView>().Count.ShouldBe(SessionView.InitialVisibleTurns);
        cut.FindComponents<UserMessageView>()[0].Instance.Block.Text.ShouldBe("question 3");
        cut.Find(ShowEarlier).TextContent.ShouldContain("3 hidden");
        cut.Find(ShowEarlier).GetAttribute("type").ShouldBe("button");
    }

    /// <summary>A transcript that fits the window has no button and shows everything.</summary>
    [Fact]
    public void ATranscriptThatFitsHasNoShowEarlierButton()
    {
        var cut = RenderSession(Turns(SessionView.InitialVisibleTurns), SessionViewState.Success);

        cut.FindAll(ShowEarlier).ShouldBeEmpty();
        cut.FindComponents<UserMessageView>().Count.ShouldBe(SessionView.InitialVisibleTurns);
    }

    /// <summary>Show earlier adds a page of turns, announces how many it added, moves focus to the first of them and does not scroll to the end.</summary>
    [Fact]
    public void ShowEarlierRevealsAnnouncesAndFocuses()
    {
        var scroll = JSInterop.SetupModule(ScrollModule);
        var transcript = JSInterop.SetupModule(TranscriptModule);
        var cut = RenderSession(Turns(SessionView.InitialVisibleTurns + 20), SessionViewState.Success);
        scroll.VerifyInvoke("scrollToEnd", 1);

        cut.Find(ShowEarlier).Click();

        cut.FindComponents<UserMessageView>().Count.ShouldBe(SessionView.InitialVisibleTurns + 20);
        cut.FindAll(ShowEarlier).ShouldBeEmpty();
        cut.Find(Status).TextContent.ShouldBe("20 earlier messages shown");
        cut.WaitForAssertion(() => transcript.VerifyInvoke("focusFirstBlock", 1));
        scroll.VerifyInvoke("scrollToEnd", 1);
    }

    /// <summary>The choice to see more survives new turns: the window does not slide back over what the user asked to see, and a second press reveals the next page.</summary>
    [Fact]
    public void ARevealedWindowDoesNotSlideWhenNewTurnsArrive()
    {
        JSInterop.SetupModule(ScrollModule);
        JSInterop.SetupModule(TranscriptModule);
        var turns = SessionView.InitialVisibleTurns + SessionView.InitialVisibleTurns + 20;
        var cut = RenderSession(Turns(turns), SessionViewState.Success);
        cut.Find(ShowEarlier).TextContent.ShouldContain("70 hidden");

        cut.Find(ShowEarlier).Click();
        cut.Find(ShowEarlier).TextContent.ShouldContain("20 hidden");
        cut.Find(Status).TextContent.ShouldBe("50 earlier messages shown");

        cut.Render(parameters => parameters.Add(p => p.Tree, Turns(turns + 1)));

        cut.Find(ShowEarlier).TextContent.ShouldContain("20 hidden");
        cut.FindComponents<UserMessageView>().Count.ShouldBe(SessionView.InitialVisibleTurns + 1 + SessionView.InitialVisibleTurns);
    }

    /// <summary>A shorter transcript in the place of the old one starts from the default window again.</summary>
    [Fact]
    public void AReplacedTranscriptStartsFromTheDefaultWindow()
    {
        JSInterop.SetupModule(ScrollModule);
        JSInterop.SetupModule(TranscriptModule);
        var cut = RenderSession(Turns(SessionView.InitialVisibleTurns + 20), SessionViewState.Success);
        cut.Find(ShowEarlier).Click();

        cut.Render(parameters => parameters.Add(p => p.Tree, Turns(SessionView.InitialVisibleTurns + 2)));

        cut.Find(ShowEarlier).TextContent.ShouldContain("2 hidden");
    }

    /// <summary>While a response streams the transcript is busy and the streamed text is not a live region; both end with the response.</summary>
    [Fact]
    public void TheTranscriptIsBusyAndSilentWhileTextStreams()
    {
        var streaming = Tree(User(0)) with { StreamingText = "partial answer" };
        var cut = RenderSession(streaming, SessionViewState.Streaming);

        cut.Find(".autonogrammer-blocks").GetAttribute("aria-busy").ShouldBe("true");
        cut.Find(".autonogrammer-streaming").GetAttribute("aria-live").ShouldBe("off");

        cut.Render(parameters => parameters
            .Add(p => p.Tree, Tree(User(0), Markdown("markdown-1", "partial answer")))
            .Add(p => p.ViewState, SessionViewState.Success));

        cut.Find(".autonogrammer-blocks").HasAttribute("aria-busy").ShouldBeFalse();
    }

    /// <summary>A finished response is announced once, with its length, by the one polite status region.</summary>
    [Fact]
    public void AFinishedResponseIsAnnouncedWithItsWordCount()
    {
        var answer = Tree(User(0), Markdown("markdown-1", "one two  three\nfour"));
        var cut = RenderSession(answer, SessionViewState.Streaming);
        cut.Find(Status).TextContent.ShouldBeEmpty();

        cut.Render(parameters => parameters.Add(p => p.ViewState, SessionViewState.Success));

        cut.FindAll(Status).Count.ShouldBe(1);
        cut.Find(Status).TextContent.ShouldBe("Assistant finished: 4 words");
    }

    /// <summary>A tool that finishes, or fails, is announced by name.</summary>
    /// <param name="status">The status the tool call reports.</param>
    /// <param name="expected">The sentence the status region speaks.</param>
    [Theory]
    [InlineData("completed", "Tool ls finished")]
    [InlineData("failed", "Tool ls failed")]
    public void AToolThatEndsIsAnnounced(string status, string expected)
    {
        var user = User(0);
        var cut = RenderSession(Tree(user, Tool("call-1", "ls", "running")), SessionViewState.Streaming);

        cut.Render(parameters => parameters.Add(p => p.Tree, Tree(user, Tool("call-1", "ls", status))));

        cut.Find(Status).TextContent.ShouldBe(expected);
    }

    /// <summary>A tool that is still running is not announced.</summary>
    [Fact]
    public void AToolThatIsStillRunningIsNotAnnounced()
    {
        var user = User(0);
        var cut = RenderSession(Tree(user, Tool("call-1", "ls", "running")), SessionViewState.Streaming);

        cut.Render(parameters => parameters.Add(p => p.Tree, Tree(user, Tool("call-1", "ls", "in_progress"))));

        cut.Find(Status).TextContent.ShouldBeEmpty();
    }

    /// <summary>A request for approval is announced with what is asked for.</summary>
    [Fact]
    public void AnApprovalRequestIsAnnounced()
    {
        var user = User(0);
        var cut = RenderSession(Tree(user), SessionViewState.Streaming);

        cut.Render(parameters => parameters.Add(p => p.Tree, Tree(user, new PermissionBlock { Id = "call-2", Prompt = "run_shell", Options = ["allow once"] })));

        cut.Find(Status).TextContent.ShouldBe("Approval needed: run_shell");
    }

    /// <summary>History that is loaded is not read out: only events that complete while the transcript is open are announced.</summary>
    [Fact]
    public void LoadedHistoryIsNotAnnounced()
    {
        var history = Tree(User(0), Tool("call-1", "ls", "completed"), new PermissionBlock { Id = "call-2", Prompt = "run_shell", Options = ["allow once"] });
        var cut = RenderSession(Tree(), SessionViewState.LoadingHistory);

        cut.Render(parameters => parameters
            .Add(p => p.Tree, history)
            .Add(p => p.ViewState, SessionViewState.Success));

        cut.Find(Status).TextContent.ShouldBeEmpty();
    }

    /// <summary>A state announcement from the host is spoken when nothing more specific completed, and a completed event wins over it.</summary>
    [Fact]
    public void TheHostAnnouncementIsSpokenUnlessAnEventCompleted()
    {
        var user = User(0);
        var cut = RenderSession(Tree(user, Tool("call-1", "ls", "running")), SessionViewState.Streaming);

        cut.Render(parameters => parameters.Add(p => p.Announcement, "Assistant is responding."));
        cut.Find(Status).TextContent.ShouldBe("Assistant is responding.");

        cut.Render(parameters => parameters
            .Add(p => p.Tree, Tree(user, Tool("call-1", "ls", "completed")))
            .Add(p => p.Announcement, "Still responding."));
        cut.Find(Status).TextContent.ShouldBe("Tool ls finished");
    }

    /// <summary>A failure the view is showing is the one assertive alert; an error further up the history is plain text.</summary>
    [Fact]
    public void OnlyTheCurrentFailureIsAnAlert()
    {
        var old = new ErrorBlock { Id = "error-1", Message = "old failure", Retryable = false };
        var current = new ErrorBlock { Id = "error-3", Message = "current failure", Retryable = true };

        var cut = RenderSession(Tree(User(0), old, User(1), current), SessionViewState.RecoverableError);

        var alerts = cut.FindAll("[role='alert']");
        alerts.Count.ShouldBe(1);
        alerts[0].TextContent.ShouldContain("current failure");
        cut.FindAll(".autonogrammer-error").Count.ShouldBe(2);
    }

    /// <summary>A code block has no live region of its own, and a copy is announced by the transcript's status region.</summary>
    [Fact]
    public void ACodeBlockHasNoLiveRegionAndACopyIsAnnouncedByTheTranscript()
    {
        var code = new CodeBlock { Id = "code-1", Language = "csharp", Code = "var x = 1;" };
        var alone = Render<CodeBlockView>(parameters => parameters.Add(p => p.Block, code));
        alone.FindAll("[aria-live]").ShouldBeEmpty();
        alone.FindAll("[role='status']").ShouldBeEmpty();

        var cut = RenderSession(Tree(User(0), code), SessionViewState.Success);
        cut.FindAll(".autonogrammer-code [aria-live]").ShouldBeEmpty();

        cut.Find("button[aria-label='Copy code']").Click();

        cut.WaitForAssertion(() => cut.Find(Status).TextContent.ShouldBe(TranscriptAnnouncements.CopiedToClipboard));
    }

    private IRenderedComponent<SessionView> RenderSession(UiTree tree, SessionViewState state) =>
        Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, tree)
            .Add(p => p.ViewState, state));

    private static UiTree Tree(params UiBlock[] blocks) => new() { Blocks = blocks };

    private static UiTree Turns(int count)
    {
        var blocks = new List<UiBlock>(count * 2);
        for (var i = 0; i < count; i++)
        {
            blocks.Add(User(i));
            blocks.Add(Markdown($"markdown-{i}", $"answer {i}"));
        }

        return Tree([.. blocks]);
    }

    private static UserMessageBlock User(int turn) => new() { Id = $"user-{turn}", Text = $"question {turn}" };

    private static MarkdownBlock Markdown(string id, string text) => new() { Id = id, Markdown = text };

    private static ToolCallBlock Tool(string id, string name, string status) => new()
    {
        Id = id,
        CallId = id,
        ToolName = name,
        Status = status,
        Arguments = EmptyObject(),
    };

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
