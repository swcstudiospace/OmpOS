using Bunit;
using OmpOS.Presentation.Components;
using OmpOS.UI.Components.Shell;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The agent rail hosts the conversation list under an Agents heading and a New conversation button, and closes the drawer on a pick (PX-08).</summary>
public sealed class AgentRailTests : UiTestContext
{
    private const string ShellModule = "./_content/OmpOS.UI/scripts/ompos-shell.js";

    private static readonly List<ConversationSummary> Conversations =
    [
        new() { Id = "s-1", Title = "First" },
        new() { Id = "s-2", Title = "Second" },
    ];

    /// <summary>The New conversation button is named "New conversation" and its visible text is contained in that name.</summary>
    [Fact]
    public void NewConversationButtonIsNamedAndLabelledConsistently()
    {
        var cut = Render<AgentRail>();

        cut.Find("h2").TextContent.ShouldBe("Agents");
        var button = cut.Find("button[aria-label='New conversation']");
        button.TextContent.Trim().ShouldBe("New conversation");
        button.ClassList.ShouldContain("ompos-btn", StringComparer.Ordinal);
        button.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
    }

    /// <summary>Rows are the existing conversation list: titles in order, the selected one marked, and a pick reported by identifier.</summary>
    [Fact]
    public void RowsReportPicksAndMarkTheSelection()
    {
        string? picked = null;
        var cut = Render<AgentRail>(parameters => parameters
            .Add(p => p.Conversations, Conversations)
            .Add(p => p.SelectedId, "s-2")
            .Add(p => p.OnSelected, (string id) => picked = id));

        cut.FindAll(".ompos-conversation-item").Select(static item => item.QuerySelector(".ompos-conversation-title")!.TextContent).ShouldBe(["First", "Second"]);
        cut.Find("button[aria-current='true']").TextContent.Trim().ShouldBe("Second");
        cut.Find("button[aria-current='true'] .ompos-status-dot").GetAttribute("data-status").ShouldBe("active");

        cut.Find(".ompos-conversation-item").Click();

        picked.ShouldBe("s-1");
    }

    /// <summary>The New conversation button reports the request to the host.</summary>
    [Fact]
    public void NewConversationButtonRaisesTheCallback()
    {
        var requests = 0;
        var cut = Render<AgentRail>(parameters => parameters.Add(p => p.OnNew, () => requests++));

        cut.Find("button[aria-label='New conversation']").Click();

        requests.ShouldBe(1);
    }

    /// <summary>Picking a conversation inside the shell closes the open drawer, so a phone user lands on the thread.</summary>
    [Fact]
    public void PickingAConversationClosesTheOpenDrawer()
    {
        JSInterop.SetupModule(ShellModule);
        string? picked = null;
        var cut = Render<AppShell>(parameters => parameters
            .Add<AgentRail>(p => p.Rail, rail => rail
                .Add(r => r.Conversations, Conversations)
                .Add(r => r.OnSelected, (string id) => picked = id)));
        cut.Find("button.ompos-topbar-menu").Click();
        cut.Instance.IsDrawerOpen.ShouldBeTrue();

        cut.Find(".ompos-conversation-item").Click();

        picked.ShouldBe("s-1");
        cut.Instance.IsDrawerOpen.ShouldBeFalse();
    }

    /// <summary>Asking for a new conversation inside the shell closes the open drawer.</summary>
    [Fact]
    public void AskingForANewConversationClosesTheOpenDrawer()
    {
        JSInterop.SetupModule(ShellModule);
        var requests = 0;
        var cut = Render<AppShell>(parameters => parameters
            .Add<AgentRail>(p => p.Rail, rail => rail.Add(r => r.OnNew, () => requests++)));
        cut.Find("button.ompos-topbar-menu").Click();

        cut.Find("button[aria-label='New conversation']").Click();

        requests.ShouldBe(1);
        cut.Instance.IsDrawerOpen.ShouldBeFalse();
    }
}
