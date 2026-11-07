using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Memory;
using Autonogrammer.Presentation.Cowork.Projects;
using Autonogrammer.UI.Components.Cowork;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork;

/// <summary>Live regions announce project, memory and instruction state changes.</summary>
public sealed class CoworkComponentTests : UiTestContext
{
    /// <summary>A project delete confirmation is announced before delete runs.</summary>
    [Fact]
    public async Task ProjectsAnnounceDeleteConfirmation()
    {
        var list = new ProjectListModel(new EmptyProjects());
        await list.LoadAsync(CancellationToken.None);
        var editor = new ProjectEditorModel(new CountingEditor());
        await editor.OpenAsync("p1", CancellationToken.None);
        var cut = Render<ProjectsView>(p => p.Add(c => c.List, list).Add(c => c.Editor, editor));
        cut.Find("[aria-live=polite]").GetAttribute("role").ShouldBe("status");
        await cut.Find("button[aria-label='Delete project']").ClickAsync();
        cut.Find("[aria-live=assertive]").TextContent.ShouldContain("2 metadata entries");
        cut.Find("[aria-live=assertive]").TextContent.ShouldContain("5 project memory entries");
        cut.Find("[role=status]").TextContent.ShouldContain("2 metadata entries");
    }

    /// <summary>An instructions save over budget is announced with the limit message.</summary>
    [Fact]
    public async Task InstructionsAnnounceSaveOutcome()
    {
        var model = new GlobalInstructionsModel(new ReadyChannel());
        await model.LoadAsync(CancellationToken.None);
        var cut = Render<InstructionsView>(p => p.Add(c => c.Model, model));
        cut.Find("[aria-live=polite]").ShouldNotBeNull();
        model.Edit(new string('z', GlobalInstructionsModel.GlobalInstructionsBytes + 1));
        await cut.Find("button[aria-label='Save instructions']").ClickAsync();
        cut.Find("[role=status]").TextContent.ShouldBe(GlobalInstructionsModel.LimitMessage);
    }

    /// <summary>Memory announces a delete and states the residual risk.</summary>
    [Fact]
    public void MemoryAnnouncesTheResidualRisk()
    {
        var list = new MemoryListModel(new EmptyMemory());
        var cut = Render<MemoryView>(p => p.Add(c => c.List, list));
        cut.Find("[aria-live=polite]").TextContent.ShouldContain("source session");
        cut.Markup.ShouldContain(MemorySettingsModel.PoisoningLimitationNotice);
    }

    private sealed class EmptyProjects : IProjectListSource
    {
        public Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, [], null, null));
    }

    private sealed class CountingEditor : IProjectEditorSource
    {
        public Task<CoworkChannelResult<ProjectListItem>> ReadAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<ProjectListItem>(true, false, new ProjectListItem(id, "Alpha", false, 2, 5), null, null));

        public Task<CoworkChannelResult<bool>> RenameAsync(string id, string name, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }

    private sealed class ReadyChannel : IGlobalInstructionsChannel
    {
        public Task<CoworkChannelResult<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<string>(true, false, "short", null, null));

        public Task<CoworkChannelResult<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }

    private sealed class EmptyMemory : IMemoryListSource
    {
        public Task<CoworkChannelResult<IReadOnlyList<MemoryListItem>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<IReadOnlyList<MemoryListItem>>(true, false, [], null, null));

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }
}
