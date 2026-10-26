using Avalonia.Automation;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Editor;

/// <summary>The find bar is keyboard reachable in order, and every editor control has an accessible name (ADE-09, XP-01).</summary>
public sealed class FindBarKeyboardTests
{
    /// <summary>Tab from the tab strip reaches the close button, the find box, Previous, Next, Match case, and then the editor.</summary>
    [Fact]
    public async Task TabFromTheTabStripReachesTheFindBarInOrderThenTheEditor()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open("notes/a.cs", "needle one\nneedle two\nthree needle\n");
            using var window = EditorHarness.Show(model);

            EditorHarness.TabToggle(window, "a.cs").Focus();
            EditorHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            EditorHarness.TabClose(window, "a.cs").IsFocused.ShouldBeTrue("the close button follows its tab");

            EditorHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            EditorHarness.FindInput(window).IsFocused.ShouldBeTrue("the find box follows the tab strip");

            EditorHarness.Type(window, "needle");
            EditorHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            EditorHarness.PreviousButton(window).IsFocused.ShouldBeTrue("Previous follows the find box once there are matches");

            EditorHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            EditorHarness.NextButton(window).IsFocused.ShouldBeTrue("Next follows Previous");

            EditorHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            EditorHarness.MatchCase(window).IsFocused.ShouldBeTrue("Match case follows Next");

            EditorHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            EditorHarness.Code(window).TextArea.IsFocused.ShouldBeTrue("the editor follows the find bar");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape in the find box returns focus to the editor, so the keyboard can leave the find bar.</summary>
    [Fact]
    public async Task EscapeInTheFindBoxReturnsFocusToTheEditor()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open("notes/a.cs", "needle one\n");
            using var window = EditorHarness.Show(model);

            EditorHarness.FindInput(window).Focus();
            EditorHarness.Press(window, Key.Escape, PhysicalKey.Escape);

            EditorHarness.Code(window).TextArea.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The tab strip, each tab, each close button, the find box, the match buttons, the status line and the editor carry names.</summary>
    [Fact]
    public async Task EveryEditorControlHasAnAccessibleName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open("notes/a.cs", "needle one\n");
            model.Open("notes/b.json", "{ }");
            using var window = EditorHarness.Show(model);

            AutomationProperties.GetName(EditorHarness.TabStrip(window)).ShouldBe("Open files");
            AutomationProperties.GetName(EditorHarness.TabToggle(window, "a.cs")).ShouldBe("a.cs");
            AutomationProperties.GetName(EditorHarness.TabClose(window, "a.cs")).ShouldBe("Close a.cs");
            AutomationProperties.GetName(EditorHarness.TabToggle(window, "b.json")).ShouldBe("b.json");
            AutomationProperties.GetName(EditorHarness.TabClose(window, "b.json")).ShouldBe("Close b.json");
            AutomationProperties.GetName(EditorHarness.FindInput(window)).ShouldBe("Find in file");
            AutomationProperties.GetName(EditorHarness.PreviousButton(window)).ShouldBe("Previous match");
            AutomationProperties.GetName(EditorHarness.NextButton(window)).ShouldBe("Next match");
            AutomationProperties.GetName(EditorHarness.MatchCase(window)).ShouldBe("Match case");
            AutomationProperties.GetName(EditorHarness.Status(window)).ShouldBe("Find result");
            AutomationProperties.GetName(EditorHarness.Code(window)).ShouldBe("Code editor");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
