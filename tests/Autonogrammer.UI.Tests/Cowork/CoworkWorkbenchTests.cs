using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.UI.Components.Cowork;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork;

/// <summary>The Cowork workbench shows signed-out, empty, failed and plain-text rows, and no Code markup.</summary>
public sealed class CoworkWorkbenchTests : UiTestContext
{
    /// <summary>A signed-out head with a sign-in route links to connect.</summary>
    [Fact]
    public void SignedOutOffersTheSignInRoute()
    {
        Services.AddSingleton<ICoworkAccess>(new FakeAccess(available: false, offersSignIn: true));

        var cut = Render<CoworkWorkbench>();

        cut.Find("p").TextContent.ShouldBe(CoworkCopy.SignInToUseCowork);
        cut.Find("a").GetAttribute("href").ShouldBe(AuthRoutes.ConnectPath);
    }

    /// <summary>The Demo shows the same sentence and no link.</summary>
    [Fact]
    public void DemoHasNoSignInLink()
    {
        Services.AddSingleton<ICoworkAccess>(new FakeAccess(available: false, offersSignIn: false));

        var cut = Render<CoworkWorkbench>();

        cut.Find("p").TextContent.ShouldBe(CoworkCopy.SignInToUseCowork);
        cut.FindAll("a").ShouldBeEmpty();
    }

    /// <summary>An available head with no rows shows the desktop sentence.</summary>
    [Fact]
    public void AnEmptyListNamesTheDesktop()
    {
        Services.AddSingleton<ICoworkAccess>(new FakeAccess(available: true, offersSignIn: false));
        using var shell = new CoworkShellModel(new MemoryCatalog());

        var cut = Render<CoworkWorkbench>(p => p.Add(c => c.Shell, shell));

        cut.Markup.ShouldContain(CoworkWorkbench.EmptyTasks);
    }

    /// <summary>A load failure is an alert with a Retry button that reloads.</summary>
    [Fact]
    public async Task ALoadFailureOffersRetry()
    {
        var access = new FakeAccess(available: true, offersSignIn: false) { LoadFailed = true };
        Services.AddSingleton<ICoworkAccess>(access);

        var cut = Render<CoworkWorkbench>();
        cut.Find("[role=alert]").TextContent.ShouldBe(CoworkWorkbench.LoadFailedMessage);

        await cut.Find("button").ClickAsync().ConfigureAwait(true);

        access.Retries.ShouldBe(1);
    }

    /// <summary>Rows are grouped by project and a title that looks like markup stays text.</summary>
    [Fact]
    public void RowsAreGroupedAndTitlesStayPlain()
    {
        Services.AddSingleton<ICoworkAccess>(new FakeAccess(available: true, offersSignIn: false));
        var catalog = new MemoryCatalog();
        catalog.Items.Add(Row("a", "<img src=x>", "proj-a"));
        catalog.Items.Add(Row("b", "Beta", "proj-b"));
        using var shell = new CoworkShellModel(catalog);

        var cut = Render<CoworkWorkbench>(p => p.Add(c => c.Shell, shell));

        cut.FindAll("img").ShouldBeEmpty();
        cut.Markup.ShouldContain("&lt;img src=x&gt;");
        cut.FindAll("h3").Select(static heading => heading.TextContent).ShouldBe(["proj-a", "proj-b"], Case.Sensitive);
    }

    /// <summary>The workbench has no h1, no view tabs and no Kanban markup, and names its three regions.</summary>
    [Fact]
    public void TheWorkbenchNamesThreeRegionsAndNoCodeChrome()
    {
        Services.AddSingleton<ICoworkAccess>(new FakeAccess(available: true, offersSignIn: false));
        var panel = new StubPanel("files", "Files");
        using var shell = new CoworkShellModel(new MemoryCatalog());
        shell.RegisterPanel(panel);
        var templates = new Dictionary<string, RenderFragment<IDockPanel>>(StringComparer.Ordinal)
        {
            ["files"] = item => builder => builder.AddContent(0, "panel-" + item.Id),
        };

        var cut = Render<CoworkWorkbench>(p => p.Add(c => c.Shell, shell).Add(c => c.PanelTemplates, templates));

        cut.FindAll("h1").ShouldBeEmpty();
        cut.FindAll("[role=tablist]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Kanban");
        cut.Find("[aria-label=Rail]").ShouldNotBeNull();
        cut.Find("#autonogrammer-conversation").GetAttribute("aria-label").ShouldBe("Conversation");
        cut.Find("[aria-label=Dock]").TextContent.ShouldContain("panel-files");
        cut.Find("[aria-label=Rail]").GetAttribute("tabindex").ShouldBe("-1");
    }

    private static CoworkSessionSummary Row(string id, string title, string project) => new()
    {
        Id = id,
        Title = title,
        Status = CoworkSessionStatus.Running,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        ProjectId = project,
    };

    private sealed class FakeAccess(bool available, bool offersSignIn) : ICoworkAccess
    {
        public event EventHandler? Changed;

        public bool IsResolved => true;

        public bool IsAvailable { get; } = available;

        public bool OffersSignIn { get; } = offersSignIn;

        public bool LoadFailed { get; set; }

        public int Retries { get; private set; }

        public Task RetryAsync()
        {
            Retries++;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryCatalog : ICoworkSessionCatalog
    {
        public event EventHandler? Changed { add { } remove { } }

        public List<CoworkSessionSummary> Items { get; } = [];

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => Items;
    }

    private sealed class StubPanel(string id, string title) : IDockPanel
    {
        public string Id { get; } = id;

        public string Title { get; } = title;

        public object Content { get; } = new();
    }
}
