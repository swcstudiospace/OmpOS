using System.Reflection;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>The shell owns the three Cowork regions and the dock registry, and has no board tab.</summary>
public sealed class CoworkShellModelTests
{
    /// <summary>The shell has rail, conversation and dock, and the dock opens and closes.</summary>
    [Fact]
    public void ThreePartsAndDockOpenClose()
    {
        var catalog = new ScriptedCatalog([]);
        using var shell = new CoworkShellModel(catalog);
        catalog.Touch();
        shell.Parts.ShouldBe([CoworkShellPart.Rail, CoworkShellPart.Conversation, CoworkShellPart.Dock]);
        shell.Rail.ShouldNotBeNull();
        shell.IsDockOpen.ShouldBeTrue();
        shell.CloseDock();
        shell.IsDockOpen.ShouldBeFalse();
        shell.OpenDock();
        shell.IsDockOpen.ShouldBeTrue();
        Names(shell).ShouldNotContain(name => name.Contains("Kanban", StringComparison.OrdinalIgnoreCase));
        Names(typeof(CoworkShellPart)).ShouldNotContain(name => name.Contains("Kanban", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Two panels show a strip; one panel does not. The registry keeps registration order.</summary>
    [Fact]
    public void OnePanelShowsNoTabStrip()
    {
        using var shell = new CoworkShellModel(new ScriptedCatalog([]));
        shell.RegisterPanel(new Panel("artifacts", "Artifacts"));
        shell.ShowTabStrip.ShouldBeFalse();
        shell.SelectedPanelId.ShouldBe("artifacts");

        shell.RegisterPanel(new Panel("browser", "Browser"));
        shell.ShowTabStrip.ShouldBeTrue();
        shell.Panels.Select(panel => panel.Id).ShouldBe(["artifacts", "browser"]);
        shell.SelectPanel("browser");
        shell.SelectedPanelId.ShouldBe("browser");

        shell.UnregisterPanel("browser");
        shell.ShowTabStrip.ShouldBeFalse();
        shell.SelectedPanelId.ShouldBe("artifacts");
    }

    private static IEnumerable<string> Names(object target) =>
        target.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(member => member.Name);

    private sealed class ScriptedCatalog(IReadOnlyList<CoworkSessionSummary> rows) : ICoworkSessionCatalog
    {
        public event EventHandler? Changed;

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => rows;

        public void Touch() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Panel(string id, string title) : IDockPanel
    {
        public string Id { get; } = id;

        public string Title { get; } = title;

        public object Content { get; } = new object();
    }
}
