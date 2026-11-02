using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The list keeps first-seen order and selection, and the toolbar enables only what the kind and state allow.</summary>
public sealed class ArtifactListModelTests
{
    private static readonly string[] OrderedIds = ["a1", "a2"];
    /// <summary>A new version replaces its row in place and never reorders the list.</summary>
    [Fact]
    public void UpsertReplacesInPlaceAndKeepsOrder()
    {
        var list = new ArtifactListModel();
        list.Upsert(Row("a1", 1));
        list.Upsert(Row("a2", 1));

        list.Upsert(Row("a1", 2));

        list.Items.Select(item => item.ArtifactId).ShouldBe(OrderedIds);
        list.Items[0].VersionCount.ShouldBe(2);
    }

    /// <summary>Removing the selected row clears the selection.</summary>
    [Fact]
    public void RemovingTheSelectedRowClearsSelection()
    {
        var list = new ArtifactListModel();
        list.Upsert(Row("a1", 1));
        list.Select("a1").ShouldBeTrue();

        list.Remove("a1");

        list.SelectedArtifactId.ShouldBeNull();
        list.Items.ShouldBeEmpty();
        list.Select("missing").ShouldBeFalse();
    }

    /// <summary>Diff needs a previous version; Copy and Edit need text; Access needs an origin; Stop needs a running surface.</summary>
    [Fact]
    public void ToolbarEnablesOnlyWhatTheKindAndStateAllow()
    {
        var image = Row("img", 1, ArtifactKind.Image, origins: ["api.example"]);

        var states = ArtifactListModel.CommandsFor(image, surfaceActive: false);

        Enabled(states, ArtifactCommand.Diff).ShouldBeFalse();
        Enabled(states, ArtifactCommand.Copy).ShouldBeFalse();
        Enabled(states, ArtifactCommand.Edit).ShouldBeFalse();
        Enabled(states, ArtifactCommand.Access).ShouldBeTrue();
        Enabled(states, ArtifactCommand.Stop).ShouldBeFalse();
        Enabled(states, ArtifactCommand.Export).ShouldBeTrue();
        Enabled(states, ArtifactCommand.Close).ShouldBeTrue();

        var text = ArtifactListModel.CommandsFor(Row("md", 3, ArtifactKind.Markdown), surfaceActive: true);
        Enabled(text, ArtifactCommand.Diff).ShouldBeTrue();
        Enabled(text, ArtifactCommand.Copy).ShouldBeTrue();
        Enabled(text, ArtifactCommand.Access).ShouldBeFalse();
        Enabled(text, ArtifactCommand.Stop).ShouldBeTrue();
    }

    /// <summary>The toolbar lists every command in the fixed order.</summary>
    [Fact]
    public void ToolbarOrderIsFixed()
    {
        ArtifactListModel.CommandsFor(Row("md", 1, ArtifactKind.Markdown), surfaceActive: false)
            .Select(state => state.Command)
            .ShouldBe(new[]
            {
                ArtifactCommand.Diff,
                ArtifactCommand.Copy,
                ArtifactCommand.Export,
                ArtifactCommand.Edit,
                ArtifactCommand.Access,
                ArtifactCommand.Stop,
                ArtifactCommand.Close,
            });
    }

    private static bool Enabled(IReadOnlyList<ArtifactCommandState> states, ArtifactCommand command) =>
        states.Single(state => state.Command == command).IsEnabled;

    private static ArtifactListItem Row(string id, int versions, ArtifactKind kind = ArtifactKind.Markdown, string[]? origins = null) =>
        new(id, "Title " + id, kind, versions, origins ?? []);
}
