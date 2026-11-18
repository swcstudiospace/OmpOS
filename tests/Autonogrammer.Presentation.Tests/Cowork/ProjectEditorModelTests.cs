using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Projects;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>The project editor: the project it shows is the one opened last, whatever order the reads finish in.</summary>
public sealed class ProjectEditorModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A read of an earlier open that finishes after a newer open is dropped, so the editor keeps the newer project.</summary>
    [Fact]
    public async Task AnOlderReadThatFinishesAfterANewerOpenIsDropped()
    {
        var source = new GatedProjectSource();
        var editor = new ProjectEditorModel(source);
        var older = editor.OpenAsync("p1", Token);
        var newer = editor.OpenAsync("p2", Token);

        source.Answer("p2", new ProjectListItem("p2", "Beta", false, 1, 2));
        await newer.ConfigureAwait(true);
        source.Answer("p1", new ProjectListItem("p1", "Alpha", false, 4, 7));
        await older.ConfigureAwait(true);

        editor.Id.ShouldBe("p2");
        editor.Name.ShouldBe("Beta");
        editor.MetadataEntries.ShouldBe(1);
        editor.ProjectMemoryEntries.ShouldBe(2);
        editor.State.ShouldBe(CoworkViewState.Ready);
    }

    /// <summary>A source whose reads finish when the test answers them.</summary>
    private sealed class GatedProjectSource : IProjectEditorSource
    {
        private readonly Dictionary<string, TaskCompletionSource<CoworkChannelResult<ProjectListItem>>> _reads = new(StringComparer.Ordinal);

        public Task<CoworkChannelResult<ProjectListItem>> ReadAsync(string id, CancellationToken cancellationToken) => Pending(id).Task;

        public void Answer(string id, ProjectListItem item) =>
            Pending(id).SetResult(new CoworkChannelResult<ProjectListItem>(true, false, item, null, null));

        public Task<CoworkChannelResult<bool>> RenameAsync(string id, string name, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        private TaskCompletionSource<CoworkChannelResult<ProjectListItem>> Pending(string id)
        {
            if (!_reads.TryGetValue(id, out var read))
            {
                read = new TaskCompletionSource<CoworkChannelResult<ProjectListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
                _reads[id] = read;
            }

            return read;
        }
    }
}
