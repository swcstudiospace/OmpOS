using Bunit;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Changes;
using OmpOS.UI.Components.Changes;
using OmpOS.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The Changes bar lists what the agent changed with Keep, Undo and Review per file and for all of them; Review opens a side panel with the diff, and a file that changed since is stale (PX-09).</summary>
public sealed class ChangesBarTests : UiTestContext
{
    private const string KeysModule = "./_content/OmpOS.UI/scripts/ompos-keys.js";

    private static FileEditBlock Edit(string path, string? oldText, string newText, string callId = "call-1") => new()
    {
        Id = callId + "-edit-0",
        CallId = callId,
        Path = path,
        OldText = oldText,
        NewText = newText,
    };

    private static (ChangeSetModel Model, MemoryWorkspace Workspace) ModelWithEdits()
    {
        var workspace = new MemoryWorkspace()
            .With("src/a.txt", "a\nB\n")
            .With("src/b.txt", "new b\n");
        var model = new ChangeSetModel(workspace);
        model.Update([Edit("src/a.txt", "a\nb\n", "a\nB\n", "c1"), Edit("src/b.txt", "old b\n", "new b\n", "c2")]);
        return (model, workspace);
    }

    private IRenderedComponent<ChangesBar> RenderBar(ChangeSetModel model) =>
        Render<ChangesBar>(parameters => parameters.Add(p => p.Model, model));

    /// <summary>With no change the bar is not drawn.</summary>
    [Fact]
    public void TheBarIsHiddenUntilTheAgentChangesAFile()
    {
        using var model = new ChangeSetModel(new MemoryWorkspace());

        var cut = RenderBar(model);

        cut.FindAll(".ompos-changes").ShouldBeEmpty();
    }

    /// <summary>Each changed file shows a kind glyph, its path, the kind and status in words, the added and removed counts, and named Keep, Undo and Review controls.</summary>
    [Fact]
    public void EachFileShowsItsKindCountsAndNamedActions()
    {
        var (model, _) = ModelWithEdits();
        using var owned = model;

        var cut = RenderBar(model);

        cut.Find("section.ompos-changes").GetAttribute("aria-label").ShouldBe(ChangeCopy.BarName);
        cut.Find(".ompos-changes-summary").TextContent.ShouldBe(model.Summary);
        cut.Find("ul.ompos-changes-list").GetAttribute("aria-label").ShouldBe(ChangeCopy.ListName);
        var rows = cut.FindAll("li.ompos-change");
        rows.Select(static row => row.QuerySelector(".ompos-change-path")!.TextContent).ShouldBe(["src/a.txt", "src/b.txt"]);
        rows[0].QuerySelector(".ompos-change-kind")!.GetAttribute("aria-hidden").ShouldBe("true");
        rows[0].QuerySelector(".ompos-change-meta")!.TextContent.ShouldBe($"{model.Entries[0].KindText}, {model.Entries[0].StatusText}");
        rows[0].QuerySelector(".ompos-change-counts")!.TextContent.ShouldBe($"{model.Entries[0].AddedText} {model.Entries[0].RemovedText}");
        rows[0].QuerySelectorAll("button").Select(static button => button.GetAttribute("aria-label")).ShouldBe(
            [model.Entries[0].KeepName, model.Entries[0].UndoName, model.Entries[0].ReviewName]);
        cut.Find($"button[aria-label='{ChangeCopy.KeepAllName}']").TextContent.ShouldBe(ChangeCopy.KeepAll);
        cut.Find($"button[aria-label='{ChangeCopy.UndoAllName}']").TextContent.ShouldBe(ChangeCopy.UndoAll);
    }

    /// <summary>Keep leaves the file, marks it kept, removes its actions and announces the result politely.</summary>
    [Fact]
    public async Task KeepMarksTheFileKeptAndAnnouncesIt()
    {
        var (model, workspace) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);

        await cut.Find($"button[aria-label='{model.Entries[0].KeepName}']").ClickAsync().ConfigureAwait(true);

        model.Entries[0].Status.ShouldBe(ChangeStatus.Kept);
        workspace.ReadFile("src/a.txt").ShouldBe("a\nB\n");
        await cut.WaitForAssertionAsync(() => cut.FindAll("li.ompos-change")[0].GetAttribute("data-status").ShouldBe("kept")).ConfigureAwait(true);
        cut.FindAll("li.ompos-change")[0].QuerySelectorAll("button").ShouldBeEmpty();
        var status = cut.Find(".ompos-sr-only[role='status']");
        status.GetAttribute("aria-live").ShouldBe("polite");
        status.TextContent.ShouldBe(model.Announcement);
    }

    /// <summary>Undo writes the text from before the agent back and marks the file undone.</summary>
    [Fact]
    public async Task UndoRestoresTheFile()
    {
        var (model, workspace) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);

        await cut.Find($"button[aria-label='{model.Entries[0].UndoName}']").ClickAsync().ConfigureAwait(true);

        workspace.ReadFile("src/a.txt").ShouldBe("a\nb\n");
        model.Entries[0].Status.ShouldBe(ChangeStatus.Undone);
        await cut.WaitForAssertionAsync(() => cut.FindAll("li.ompos-change")[0].GetAttribute("data-status").ShouldBe("undone")).ConfigureAwait(true);
    }

    /// <summary>Keep all and Undo all act on every file that waits, and are disabled once none waits.</summary>
    [Fact]
    public async Task KeepAllResolvesEveryFileAndDisablesTheBulkActions()
    {
        var (model, _) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);
        cut.Find($"button[aria-label='{ChangeCopy.KeepAllName}']").HasAttribute("aria-disabled").ShouldBeFalse();

        await cut.Find($"button[aria-label='{ChangeCopy.KeepAllName}']").ClickAsync().ConfigureAwait(true);

        model.Entries.ShouldAllBe(static entry => entry.Status == ChangeStatus.Kept);
        await cut.WaitForAssertionAsync(() => cut.Find($"button[aria-label='{ChangeCopy.KeepAllName}']").GetAttribute("aria-disabled").ShouldBe("true")).ConfigureAwait(true);
        cut.Find($"button[aria-label='{ChangeCopy.UndoAllName}']").GetAttribute("aria-disabled").ShouldBe("true");
    }

    /// <summary>A file that changed since the agent edited it is stale: nothing is written, the row says to review it by hand, and its actions stay.</summary>
    [Fact]
    public async Task AStaleFileIsNotWrittenAndSaysSo()
    {
        var (model, workspace) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);
        workspace.With("src/a.txt", "a\nsomeone else\n");

        await cut.Find($"button[aria-label='{model.Entries[0].UndoName}']").ClickAsync().ConfigureAwait(true);

        workspace.ReadFile("src/a.txt").ShouldBe("a\nsomeone else\n");
        await cut.WaitForAssertionAsync(() =>
        {
            var row = cut.FindAll("li.ompos-change")[0];
            row.GetAttribute("data-status").ShouldBe("stale");
            row.QuerySelector(".ompos-change-message")!.TextContent.ShouldContain(ChangeCopy.StaleMessage, Case.Sensitive);
            row.QuerySelectorAll("button").Count.ShouldBe(3);
        }).ConfigureAwait(true);
    }

    /// <summary>Review opens a side panel with one section per hunk and a real diff; each hunk can be accepted or rejected, and applying writes the rejected hunk back.</summary>
    [Fact]
    public async Task ReviewOpensASidePanelAndApplyWritesTheDecisions()
    {
        var (model, workspace) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);

        await cut.Find($"button[aria-label='{model.Entries[0].ReviewName}']").ClickAsync().ConfigureAwait(true);

        var panel = cut.Find("aside.ompos-review-panel");
        panel.GetAttribute("aria-labelledby").ShouldBe(cut.Find(".ompos-review-title").Id);
        cut.Find(".ompos-review-title").TextContent.ShouldBe("src/a.txt");
        cut.FindAll("section.ompos-review-hunk").Count.ShouldBe(1);
        cut.Find("tr.ompos-diff-line[data-kind='added'] code").TextContent.ShouldBe("B");
        cut.FindAll(".ompos-diff-view .ompos-segmented").ShouldBeEmpty();

        await cut.Find("button[aria-label='Reject hunk 1']").ClickAsync().ConfigureAwait(true);
        cut.Find("button[aria-label='Reject hunk 1']").GetAttribute("aria-pressed").ShouldBe("true");
        cut.Find(".ompos-review-hunk-decision").TextContent.ShouldBe("Rejected");
        await cut.Find($"button[aria-label='{ChangeCopy.ApplyReviewName}']").ClickAsync().ConfigureAwait(true);

        workspace.ReadFile("src/a.txt").ShouldBe("a\nb\n");
        model.Entries[0].Status.ShouldBe(ChangeStatus.Undone);
        await cut.WaitForAssertionAsync(() => cut.FindAll("aside.ompos-review-panel").ShouldBeEmpty()).ConfigureAwait(true);
    }

    /// <summary>Escape closes the review panel and moves the focus back to the Review button that opened it.</summary>
    [Fact]
    public async Task EscapeClosesTheReviewAndReturnsTheFocus()
    {
        var keys = JSInterop.SetupModule(KeysModule);
        var (model, _) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);
        await cut.Find($"button[aria-label='{model.Entries[1].ReviewName}']").ClickAsync().ConfigureAwait(true);
        cut.Find("aside.ompos-review-panel");

        await cut.Find("aside.ompos-review-panel").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);

        cut.FindAll("aside.ompos-review-panel").ShouldBeEmpty();
        await cut.WaitForAssertionAsync(() => keys.VerifyInvoke("focusItem").Arguments[1].ShouldBe("src/b.txt")).ConfigureAwait(true);
    }

    /// <summary>The close button of the panel is named, and the heading takes the focus when the panel opens.</summary>
    [Fact]
    public async Task TheReviewPanelIsNamedAndTakesTheFocus()
    {
        var (model, _) = ModelWithEdits();
        using var owned = model;
        var cut = RenderBar(model);

        await cut.Find($"button[aria-label='{model.Entries[0].ReviewName}']").ClickAsync().ConfigureAwait(true);

        cut.Find("button[aria-label='Close review']").ShouldNotBeNull();
        JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0].ShouldBeElementReferenceTo(cut.Find(".ompos-review-title"));
        foreach (var button in cut.FindAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent).Trim().ShouldNotBeEmpty();
        }
    }
}
