using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>Editor tabs: opening, selecting and closing, with paths normalised to one tab per file (ADE-09).</summary>
public sealed class EditorTabsTests
{
    /// <summary>Opening a file adds a tab and selects it, and the tab names the file.</summary>
    [Fact]
    public void Open_AddsATabAndSelectsIt()
    {
        var tabs = new EditorTabs();

        var tab = tabs.Open("src/Program.cs", "class Program {}");

        tabs.Tabs.Count.ShouldBe(1);
        tabs.SelectedTab.ShouldBeSameAs(tab);
        tab.Name.ShouldBe("Program.cs");
        tab.Document.Text.ShouldBe("class Program {}");
    }

    /// <summary>Opening a file that already has a tab selects that tab and keeps its unsaved buffer.</summary>
    [Fact]
    public void Open_OfAnOpenFile_SelectsTheExistingTabAndKeepsItsBuffer()
    {
        var tabs = new EditorTabs();
        var first = tabs.Open("a.txt", "one");
        tabs.Open("b.txt", "two");
        first.Document.Replace(0, 3, "edited");

        var reopened = tabs.Open("a.txt", "fresh from disk");

        tabs.Tabs.Count.ShouldBe(2);
        reopened.ShouldBeSameAs(first);
        tabs.SelectedTab.ShouldBeSameAs(first);
        first.Document.Text.ShouldBe("edited");
    }

    /// <summary>Two spellings of one path name the same tab.</summary>
    [Fact]
    public void Open_NormalisesThePath_SoOneFileHasOneTab()
    {
        var tabs = new EditorTabs();

        tabs.Open("src//Program.cs", "x");
        var tab = tabs.Open("./src/Program.cs", "y");

        tabs.Tabs.Count.ShouldBe(1);
        tab.RelativePath.ShouldBe("src/Program.cs");
    }

    /// <summary>Select switches the selected tab to an open file and refuses a file that is not open.</summary>
    [Fact]
    public void Select_SwitchesToAnOpenTabAndRefusesAClosedOne()
    {
        var tabs = new EditorTabs();
        var a = tabs.Open("a.txt", "a");
        tabs.Open("b.txt", "b");

        tabs.Select("a.txt");

        tabs.SelectedTab.ShouldBeSameAs(a);
        Should.Throw<ArgumentException>(() => tabs.Select("missing.txt"));
    }

    /// <summary>Closing the selected tab selects the tab that takes its place.</summary>
    [Fact]
    public void Close_SelectedTab_SelectsTheTabThatTakesItsPlace()
    {
        var tabs = new EditorTabs();
        tabs.Open("a.txt", "a");
        var b = tabs.Open("b.txt", "b");
        var c = tabs.Open("c.txt", "c");
        tabs.Select("b.txt");

        tabs.Close("b.txt").ShouldBeTrue();

        tabs.Tabs.ShouldBe([tabs.Tabs[0], c]);
        tabs.SelectedTab.ShouldBeSameAs(c);
        tabs.Tabs.ShouldNotContain(b);
    }

    /// <summary>Closing the last tab, when it is selected, selects the tab before it.</summary>
    [Fact]
    public void Close_LastSelectedTab_SelectsThePreviousTab()
    {
        var tabs = new EditorTabs();
        var a = tabs.Open("a.txt", "a");
        tabs.Open("b.txt", "b");
        tabs.Open("c.txt", "c");

        tabs.Close("c.txt");

        tabs.SelectedTab.ShouldNotBeNull().RelativePath.ShouldBe("b.txt");
        tabs.Tabs[0].ShouldBeSameAs(a);
    }

    /// <summary>Closing a tab that is not selected leaves the selection alone; closing an unknown file reports false.</summary>
    [Fact]
    public void Close_UnselectedTab_KeepsTheSelection()
    {
        var tabs = new EditorTabs();
        tabs.Open("a.txt", "a");
        var b = tabs.Open("b.txt", "b");

        tabs.Close("a.txt").ShouldBeTrue();
        tabs.Close("never-opened.txt").ShouldBeFalse();

        tabs.SelectedTab.ShouldBeSameAs(b);
    }

    /// <summary>Closing the only tab leaves no tab and no selection.</summary>
    [Fact]
    public void Close_TheOnlyTab_LeavesNoSelection()
    {
        var tabs = new EditorTabs();
        tabs.Open("a.txt", "a");

        tabs.Close("a.txt");

        tabs.Tabs.ShouldBeEmpty();
        tabs.SelectedTab.ShouldBeNull();
    }

    /// <summary>A tab needs a file: the workspace root and paths that escape it are refused, and nothing opens.</summary>
    [Fact]
    public void Open_RefusesTheRootAndEscapingPaths()
    {
        var tabs = new EditorTabs();

        Should.Throw<ArgumentException>(() => tabs.Open(string.Empty, "x"));
        Should.Throw<ArgumentException>(() => tabs.Open("../secret.txt", "x"));
        tabs.Tabs.ShouldBeEmpty();
    }

    /// <summary>Changing the selected tab is announced.</summary>
    [Fact]
    public void SelectedTab_AnnouncesItsChange()
    {
        var tabs = new EditorTabs();
        tabs.Open("a.txt", "a");
        var announced = PropertyChangeRecorder.Record(tabs);

        tabs.Open("b.txt", "b");

        announced.ShouldAnnounce(nameof(EditorTabs.SelectedTab));
    }
}
