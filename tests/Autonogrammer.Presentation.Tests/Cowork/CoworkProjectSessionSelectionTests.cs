using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Projects;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>The project list's selection: only an active row can be selected, a reload keeps it while the row stays, and a clear drops it with any stale load.</summary>
public sealed class CoworkProjectSessionSelectionTests
{
    private static readonly ProjectListItem Alpha = new("p-a", "Alpha", false, 1, 0);
    private static readonly ProjectListItem Old = new("p-old", "Old", true, 1, 0);

    /// <summary>An active row selects; an archived or unknown identifier is refused and leaves the selection unchanged.</summary>
    [Fact]
    public async Task OnlyAnActiveRowCanBeSelected()
    {
        var source = new ScriptedProjects { List = Ok(Alpha, Old) };
        var list = new ProjectListModel(source);
        await list.LoadAsync(CancellationToken.None);

        list.Select(Old.Id).ShouldBeFalse();
        list.Select("p-missing").ShouldBeFalse();
        list.SelectedId.ShouldBeNull();

        list.Select(Alpha.Id).ShouldBeTrue();
        list.SelectedId.ShouldBe(Alpha.Id);
        list.Selected.ShouldBe(Alpha);

        list.Select(null).ShouldBeTrue();
        list.SelectedId.ShouldBeNull();
    }

    /// <summary>A reload keeps the selection while the row is still there, and drops it when the row leaves the list.</summary>
    [Fact]
    public async Task AReloadKeepsTheSelectionOnlyWhileTheRowStays()
    {
        var source = new ScriptedProjects { List = Ok(Alpha) };
        var list = new ProjectListModel(source);
        await list.LoadAsync(CancellationToken.None);
        list.Select(Alpha.Id).ShouldBeTrue();

        await list.LoadAsync(CancellationToken.None);
        list.SelectedId.ShouldBe(Alpha.Id);

        source.List = Ok(Old);
        await list.LoadAsync(CancellationToken.None);
        list.SelectedId.ShouldBeNull();
        list.Selected.ShouldBeNull();
    }

    /// <summary>A row that was archived after it was selected is dropped by the next load.</summary>
    [Fact]
    public async Task AnArchivedRowLeavesTheSelection()
    {
        var source = new ScriptedProjects { List = Ok(Alpha) };
        var list = new ProjectListModel(source);
        await list.LoadAsync(CancellationToken.None);
        list.Select(Alpha.Id).ShouldBeTrue();

        source.List = Ok(Alpha with { Archived = true });
        await list.LoadAsync(CancellationToken.None);

        list.SelectedId.ShouldBeNull();
    }

    /// <summary>Clear drops the rows and the selection, and a load still in flight when it runs never applies its rows.</summary>
    [Fact]
    public async Task ClearDropsTheSelectionAndAStaleLoadNeverApplies()
    {
        var source = new ScriptedProjects { List = Ok(Alpha) };
        var list = new ProjectListModel(source);
        await list.LoadAsync(CancellationToken.None);
        list.Select(Alpha.Id).ShouldBeTrue();

        var gate = new TaskCompletionSource<CoworkChannelResult<IReadOnlyList<ProjectListItem>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Pending = gate.Task;
        var inFlight = list.LoadAsync(CancellationToken.None);

        list.Clear(CoworkDegradedSentences.SignedOut);
        list.SelectedId.ShouldBeNull();
        list.Rows.ShouldBeEmpty();
        list.State.ShouldBe(CoworkViewState.Empty);
        list.Sentence.ShouldBe(CoworkDegradedSentences.SignedOut);

        gate.SetResult(Ok(Alpha, Old));
        await inFlight.ConfigureAwait(true);

        list.State.ShouldBe(CoworkViewState.Empty);
        list.Rows.ShouldBeEmpty();
        list.SelectedId.ShouldBeNull();
        list.Select(Alpha.Id).ShouldBeFalse();
    }

    /// <summary>A load from the local copy carries its sync state as the sentence, so a read-only copy is labelled.</summary>
    [Fact]
    public async Task AReadOnlyLocalCopyCarriesItsSyncSentence()
    {
        var source = new ScriptedProjects
        {
            List = new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, [Alpha], "RateLimited", null),
        };
        var list = new ProjectListModel(source);

        await list.LoadAsync(CancellationToken.None);

        list.State.ShouldBe(CoworkViewState.Ready);
        list.Rows.ShouldBe([Alpha]);
        list.Sentence.ShouldBe(CoworkDegradedSentences.RateLimited);
    }

    private static CoworkChannelResult<IReadOnlyList<ProjectListItem>> Ok(params ProjectListItem[] rows) =>
        new(true, false, rows, "Synced", null);

    private sealed class ScriptedProjects : IProjectListSource
    {
        public CoworkChannelResult<IReadOnlyList<ProjectListItem>> List { get; set; } = Ok();

        public Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>>? Pending { get; set; }

        public Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken)
        {
            var pending = Pending;
            Pending = null;
            return pending ?? Task.FromResult(List);
        }
    }
}
