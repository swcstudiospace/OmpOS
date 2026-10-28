using Bunit;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.Presentation.Settings;
using OmpOS.UI.Components.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The web Settings dialog: a modal dialog with a focus trap, Escape, focus return and the unsaved-changes prompt of the desktop window, a navigation in desktop order, and one page at a time (PX-09).</summary>
public sealed class SettingsDialogTests : UiTestContext
{
    private const string DialogModule = "./_content/OmpOS.UI/scripts/ompos-dialog.js";
    private const int TrapToken = 3;

    /// <summary>A closed dialog renders nothing and loads nothing.</summary>
    [Fact]
    public void ClosedDialogRendersNothing()
    {
        var module = SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);

        var cut = RenderDialog(window, isOpen: false);

        cut.Markup.Trim().ShouldBeEmpty();
        module.Invocations.Count.ShouldBe(0);
    }

    /// <summary>An open dialog is a named modal dialog; its navigation lists the pages in window order with the first one selected, and it shows that page.</summary>
    [Fact]
    public void OpenDialogIsANamedModalWithTheNavigationInWindowOrder()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);

        var cut = RenderDialog(window);

        var dialog = cut.Find("[role='dialog']");
        dialog.GetAttribute("aria-modal").ShouldBe("true");
        cut.Find("#" + dialog.GetAttribute("aria-labelledby")).TextContent.ShouldBe(SettingsCopy.WindowTitle);
        var nav = cut.Find("nav");
        nav.GetAttribute("aria-label").ShouldBe(SettingsCopy.NavigationName);
        nav.QuerySelectorAll("button").Select(static button => button.TextContent.Trim()).ShouldBe(["Runtime", "General", "Agents"]);
        nav.QuerySelectorAll("button[aria-current='page']").Select(static button => button.TextContent.Trim()).ShouldBe(["Runtime"]);
        cut.Find("h3").TextContent.ShouldBe(SettingsCopy.RuntimeHeading);
    }

    /// <summary>Opening traps the focus in the dialog and moves it there, and opening loads every page once.</summary>
    [Fact]
    public void OpeningTrapsFocusMovesItIntoTheDialogAndLoadsThePages()
    {
        var module = SetupDialogModule();
        var window = SettingsFixtures.Window(out var general, out _, []);

        var cut = RenderDialog(window);

        cut.WaitForAssertion(() =>
        {
            var trap = module.VerifyInvoke("trapFocus");
            trap.Arguments.Count.ShouldBe(1);
            JSInterop.FocusCalls().ShouldHaveSingleItem().ReferenceIdOf().ShouldBe(trap.ReferenceIdOf());
            cut.Find("[role='dialog']").GetAttribute("tabindex").ShouldBe("-1");
            window.Pages.OfType<GeneralPage>().Single().State.ShouldBe(SettingsPageState.Ready);
        });
        general.Saved.ShouldBeEmpty();
    }

    /// <summary>Selecting a navigation entry shows that page and marks the entry current.</summary>
    [Fact]
    public void NavigationSelectsThePageToShow()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);
        var cut = RenderDialog(window);

        cut.FindAll("nav button").Single(static button => string.Equals(button.TextContent.Trim(), "General", StringComparison.Ordinal)).Click();

        cut.Find("h3").TextContent.ShouldBe(SettingsCopy.GeneralHeading);
        cut.Find("nav button[aria-current='page']").TextContent.Trim().ShouldBe("General");
        window.SelectedSection.ShouldBe(SettingsSection.General);
    }

    /// <summary>Escape, the Close button and the scrim each close a dialog with nothing unsaved, tell the host, release the trap and drop the markup.</summary>
    [Theory]
    [InlineData("escape")]
    [InlineData("button")]
    [InlineData("scrim")]
    public void EveryCloseControlClosesAndReleasesTheTrap(string how)
    {
        var module = SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);
        var changes = new List<bool>();
        var cut = RenderDialog(window, onChange: changes.Add);

        switch (how)
        {
            case "escape":
                cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
                break;
            case "button":
                cut.Find("button[aria-label='" + SettingsWebCopy.CloseName + "']").Click();
                break;
            default:
                cut.Find(".ompos-settings-scrim").Click();
                break;
        }

        changes.ShouldBe([false]);
        cut.FindAll("[role='dialog']").Count.ShouldBe(0);
        module.VerifyInvoke("releaseFocus").Arguments.ShouldBe([TrapToken]);
    }

    /// <summary>Other keys leave the dialog open.</summary>
    [Fact]
    public void OtherKeysDoNotClose()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);
        var changes = new List<bool>();
        var cut = RenderDialog(window, onChange: changes.Add);

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        changes.ShouldBeEmpty();
        cut.FindAll("[role='dialog']").Count.ShouldBe(1);
    }

    /// <summary>Closing with unsaved edits keeps the dialog open and asks; the page behind the prompt is inert, and the nav entry carries the unsaved state in its name.</summary>
    [Fact]
    public void ClosingWithUnsavedEditsShowsThePromptInsteadOfClosing()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);
        var changes = new List<bool>();
        var cut = RenderDialog(window, onChange: changes.Add);
        ToggleAutoRun(cut);
        cut.Find("nav button[aria-label='Agents, unsaved changes']").ShouldNotBeNull();

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        changes.ShouldBeEmpty();
        var prompt = cut.Find("[role='alertdialog']");
        prompt.GetAttribute("aria-modal").ShouldBe("true");
        cut.Find("#" + prompt.GetAttribute("aria-labelledby")).TextContent.ShouldBe(SettingsCopy.ClosePromptHeading);
        cut.Find("#" + prompt.GetAttribute("aria-describedby")).TextContent.ShouldBe(SettingsCopy.ClosePromptBody);
        cut.Find(".ompos-settings-body").HasAttribute("inert").ShouldBeTrue();
        prompt.QuerySelectorAll("button").Select(static button => button.TextContent).ShouldBe(
            [SettingsCopy.ClosePromptSaveLabel, SettingsCopy.ClosePromptDiscardLabel, SettingsCopy.ClosePromptKeepEditingLabel]);
        cut.WaitForAssertion(() => JSInterop.FocusCalls()[^1].Arguments[0].ShouldBeElementReferenceTo(cut.Find("[role='alertdialog'] button")));
    }

    /// <summary>Keep editing, and Escape on the prompt, dismiss it and leave the dialog and the edits as they were.</summary>
    [Theory]
    [InlineData("button")]
    [InlineData("escape")]
    public void KeepEditingDismissesThePrompt(string how)
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out var agents, []);
        var changes = new List<bool>();
        var cut = RenderDialog(window, onChange: changes.Add);
        ToggleAutoRun(cut);
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        if (string.Equals(how, "button", StringComparison.Ordinal))
        {
            cut.FindAll("[role='alertdialog'] button").Single(static button => string.Equals(button.TextContent, SettingsCopy.ClosePromptKeepEditingLabel, StringComparison.Ordinal)).Click();
        }
        else
        {
            cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        }

        cut.FindAll("[role='alertdialog']").Count.ShouldBe(0);
        cut.FindAll("[role='dialog']").Count.ShouldBe(1);
        changes.ShouldBeEmpty();
        window.HasUnsavedChanges.ShouldBeTrue();
        agents.Saved.ShouldBeEmpty();
        cut.Find("button[role='switch']").GetAttribute("aria-checked").ShouldBe("true");
    }

    /// <summary>Discard and close throws the edits away and closes.</summary>
    [Fact]
    public void DiscardAndCloseThrowsTheEditsAwayAndCloses()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out var agents, []);
        var changes = new List<bool>();
        var cut = RenderDialog(window, onChange: changes.Add);
        ToggleAutoRun(cut);
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[role='alertdialog'] button").Single(static button => string.Equals(button.TextContent, SettingsCopy.ClosePromptDiscardLabel, StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => changes.ShouldBe([false]));
        window.HasUnsavedChanges.ShouldBeFalse();
        agents.Saved.ShouldBeEmpty();
        cut.FindAll("[role='dialog']").Count.ShouldBe(0);
    }

    /// <summary>Save and close writes every dirty page through its source and closes.</summary>
    [Fact]
    public void SaveAndCloseWritesTheDirtyPagesAndCloses()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out var agents, []);
        var changes = new List<bool>();
        var cut = RenderDialog(window, onChange: changes.Add);
        ToggleAutoRun(cut);
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("[role='alertdialog'] button").Single(static button => string.Equals(button.TextContent, SettingsCopy.ClosePromptSaveLabel, StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => changes.ShouldBe([false]));
        agents.Saved.ShouldBe([new AgentsSettingsValues(TerminalAutoRun: true)]);
        window.HasUnsavedChanges.ShouldBeFalse();
    }

    /// <summary>A page the web head has no view for still gets a page: its title and its state.</summary>
    [Fact]
    public void UnknownPageFallsBackToATitleAndState()
    {
        SetupDialogModule();
        var window = new SettingsWindowModel([new SettingsFixtures.UnknownPage()]);

        var cut = RenderDialog(window);

        cut.Find("h3").TextContent.ShouldBe("Commands");
        cut.Find("[data-state]").TextContent.ShouldContain(SettingsWebCopy.GenericState);
        cut.Find(".ompos-settings-page-description").TextContent.ShouldBe(SettingsWebCopy.GenericUnavailable);
    }

    /// <summary>Every dialog control has an accessible name and the markup carries no inline script or style.</summary>
    [Fact]
    public void EveryControlIsNamedAndNothingIsInline()
    {
        SetupDialogModule();
        var window = SettingsFixtures.Window(out _, out _, []);
        var cut = RenderDialog(window);

        foreach (var element in cut.FindAll("button, input, select, textarea"))
        {
            var named = element.HasAttribute("aria-label") || element.TextContent.Trim().Length > 0 || cut.FindAll("label[for='" + element.Id + "']").Count > 0;
            named.ShouldBeTrue("a control has no accessible name: " + element.OuterHtml);
        }

        cut.Markup.ShouldNotContain("<script", Case.Insensitive);
        cut.FindAll("[style]").Count.ShouldBe(0);
    }

    private static void ToggleAutoRun(IRenderedComponent<SettingsDialog> cut)
    {
        cut.FindAll("nav button").Single(static button => string.Equals(button.TextContent.Trim(), "Agents", StringComparison.Ordinal)).Click();
        cut.WaitForAssertion(() => cut.Find("button[role='switch']").ShouldNotBeNull());
        cut.Find("button[role='switch']").Click();
    }

    private BunitJSModuleInterop SetupDialogModule()
    {
        var module = JSInterop.SetupModule(DialogModule);
        module.Setup<int>("trapFocus", _ => true).SetResult(TrapToken);
        return module;
    }

    private IRenderedComponent<SettingsDialog> RenderDialog(SettingsWindowModel window, bool isOpen = true, Action<bool>? onChange = null) =>
        Render<SettingsDialog>(parameters =>
        {
            parameters.Add(p => p.Settings, window).Add(p => p.IsOpen, isOpen);
            if (onChange is not null)
            {
                parameters.Add(p => p.IsOpenChanged, onChange);
            }
        });
}
