using System.Text;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Memory;
using Autonogrammer.Presentation.Cowork.Projects;
using Autonogrammer.Presentation.Cowork.Scheduling;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>Loading, empty, offline, error and ready for projects, memory and global instructions.</summary>
public sealed class CoworkPresentationTests
{
    private const string Canary = "CANARY-INSTRUCTION-TEXT";

    /// <summary>Every D-29 state has a sentence.</summary>
    [Fact]
    public void EveryDegradedStateHasASentence()
    {
        foreach (var state in CoworkDegradedSentences.States)
        {
            CoworkDegradedSentences.For(state).ShouldNotBeNullOrWhiteSpace();
        }

        CoworkDegradedSentences.States.Count.ShouldBe(21);
    }

    /// <summary>Global instructions cover loading, empty, offline, error and ready, and refuse an over-budget save.</summary>
    [Fact]
    public async Task GlobalInstructionsCoverTheFiveStatesAndTheBudget()
    {
        var channel = new ScriptedInstructions();
        var model = new GlobalInstructionsModel(channel);
        model.State.ShouldBe(CoworkViewState.Loading);

        channel.Load = new CoworkChannelResult<string>(true, false, string.Empty, null, null);
        await model.LoadAsync(CancellationToken.None);
        model.State.ShouldBe(CoworkViewState.Empty);

        channel.Load = new CoworkChannelResult<string>(true, false, "Keep answers short.", null, null);
        await model.LoadAsync(CancellationToken.None);
        model.State.ShouldBe(CoworkViewState.Ready);

        channel.Load = new CoworkChannelResult<string>(false, true, null, "Offline", null);
        await model.LoadAsync(CancellationToken.None);
        model.State.ShouldBe(CoworkViewState.Offline);
        model.Sentence.ShouldBe(CoworkDegradedSentences.Offline);
        model.IsReadOnly.ShouldBeTrue();

        channel.Load = new CoworkChannelResult<string>(false, false, null, "TokenExpired", null);
        await model.LoadAsync(CancellationToken.None);
        model.State.ShouldBe(CoworkViewState.Error);
        model.Sentence.ShouldBe(CoworkDegradedSentences.TokenExpired);

        channel.Load = new CoworkChannelResult<string>(false, false, null, "ApiDisabled", null);
        await model.LoadAsync(CancellationToken.None);
        model.Sentence.ShouldBe(CoworkDegradedSentences.ApiDisabled);

        channel.Load = new CoworkChannelResult<string>(true, false, Canary, null, null);
        await model.LoadAsync(CancellationToken.None);
        model.Edit(new string('x', GlobalInstructionsModel.GlobalInstructionsBytes + 1));
        var saved = await model.SaveAsync(CancellationToken.None);
        saved.ShouldBeFalse();
        model.Sentence.ShouldBe(GlobalInstructionsModel.LimitMessage);
        channel.Saved.ShouldBeNull();
        model.ToString().ShouldNotContain(Canary);
        string.Join('\n', model.OutcomeLog).ShouldNotContain(Canary);
        Encoding.UTF8.GetByteCount(model.Draft).ShouldBeGreaterThan(GlobalInstructionsModel.GlobalInstructionsBytes);
    }

    /// <summary>A save under the budget calls the channel and logs only the length.</summary>
    [Fact]
    public async Task GlobalInstructionsSaveUnderBudget()
    {
        var channel = new ScriptedInstructions
        {
            Load = new CoworkChannelResult<string>(true, false, "ok", null, null),
            Save = new CoworkChannelResult<bool>(true, false, true, null, null),
        };
        var model = new GlobalInstructionsModel(channel);
        await model.LoadAsync(CancellationToken.None);
        model.Edit("short");
        (await model.SaveAsync(CancellationToken.None)).ShouldBeTrue();
        channel.Saved.ShouldBe("short");
        model.OutcomeLog.ShouldContain(static line => line.Contains("outcome=saved", StringComparison.Ordinal));
    }

    /// <summary>Projects cover loading, empty, offline, error and ready.</summary>
    [Fact]
    public async Task ProjectsCoverTheFiveStates()
    {
        var source = new ScriptedProjects();
        var list = new ProjectListModel(source);
        list.State.ShouldBe(CoworkViewState.Loading);

        source.List = new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, [], null, null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Empty);

        var row = new ProjectListItem("p1", "Alpha", false, 2, 3);
        source.List = new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, [row], null, null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Ready);

        source.List = new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(false, true, null, "Offline", null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Offline);
        list.Sentence.ShouldBe(CoworkDegradedSentences.Offline);

        source.List = new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(false, false, null, "RateLimited", null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Error);
        list.Sentence.ShouldBe(CoworkDegradedSentences.RateLimited);
    }

    /// <summary>The delete confirmation carries both counts before delete runs.</summary>
    [Fact]
    public async Task DeleteConfirmationCarriesBothCountsBeforeDelete()
    {
        var source = new ScriptedEditor
        {
            Read = new CoworkChannelResult<ProjectListItem>(true, false, new ProjectListItem("p1", "Alpha", false, 4, 7), null, null),
            Delete = new CoworkChannelResult<bool>(true, false, true, null, null),
        };
        var editor = new ProjectEditorModel(source);
        await editor.OpenAsync("p1", CancellationToken.None);
        editor.PrepareDelete();
        editor.DeleteRan.ShouldBeFalse();
        editor.MetadataEntries.ShouldBe(4);
        editor.ProjectMemoryEntries.ShouldBe(7);
        editor.DeleteConfirmation.ShouldContain("4 metadata entries");
        editor.DeleteConfirmation.ShouldContain("7 project memory entries");
        source.Deleted.ShouldBeFalse();
        (await editor.DeleteAsync(CancellationToken.None)).ShouldBeTrue();
        editor.DeleteRan.ShouldBeTrue();
        source.Deleted.ShouldBeTrue();
    }

    /// <summary>Memory covers the five states and names the source session on delete.</summary>
    [Fact]
    public async Task MemoryCoversTheFiveStatesAndNamesTheSourceSession()
    {
        var source = new ScriptedMemory();
        var list = new MemoryListModel(source);
        list.State.ShouldBe(CoworkViewState.Loading);

        source.List = new CoworkChannelResult<IReadOnlyList<MemoryListItem>>(true, false, [], null, null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Empty);

        source.List = new CoworkChannelResult<IReadOnlyList<MemoryListItem>>(true, false, [new MemoryListItem("m1", "note", "session-9")], null, null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Ready);
        list.Rows[0].SourceSession.ShouldBe("session-9");

        source.List = new CoworkChannelResult<IReadOnlyList<MemoryListItem>>(false, true, null, "Offline", null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Offline);

        source.List = new CoworkChannelResult<IReadOnlyList<MemoryListItem>>(false, false, null, "MemoryPaused", null);
        await list.LoadAsync(CancellationToken.None);
        list.State.ShouldBe(CoworkViewState.Error);
        list.Sentence.ShouldBe(CoworkDegradedSentences.MemoryPaused);
    }

    /// <summary>Settings expose the three flags and the poisoning sentences.</summary>
    [Fact]
    public async Task MemorySettingsExposeFlagsAndNotices()
    {
        var source = new ScriptedSettings
        {
            Load = new CoworkChannelResult<MemorySettingsSnapshot>(true, false, new MemorySettingsSnapshot(true, false, false), null, null),
        };
        var model = new MemorySettingsModel(source);
        await model.LoadAsync(CancellationToken.None);
        model.State.ShouldBe(CoworkViewState.Ready);
        model.Enabled.ShouldBeTrue();
        MemorySettingsModel.PoisoningLimitationNotice.ShouldContain("English-only");
        MemorySettingsModel.ResidualRiskSentence.ShouldContain("source session");
    }

    /// <summary>A session task carries the Automations settings fields and schedule wording.</summary>
    [Fact]
    public void SessionTaskCarriesTargetsAndScheduleWording()
    {
        var task = new SessionTaskModel
        {
            Surface = "Cowork",
            ProjectId = "p1",
            TrustedFolders = ["/work"],
            ApprovalMode = "Manual",
            SchedulePreset = "Weekdays",
            MissedRunPolicy = "RunOnce",
            MissedRuns = 2,
        };
        task.ApplyState("ScheduleLate");
        task.Sentence.ShouldBe(CoworkDegradedSentences.ScheduleLate);
        task.NextRunText.ShouldContain("Next run");
        task.MissedRunsText.ShouldContain("2");
        SessionTaskModel.DesktopOnlySentence.ShouldContain("desktop");
    }

    private sealed class ScriptedInstructions : IGlobalInstructionsChannel
    {
        public CoworkChannelResult<string> Load { get; set; } = new(true, false, string.Empty, null, null);

        public CoworkChannelResult<bool> Save { get; set; } = new(true, false, true, null, null);

        public string? Saved { get; private set; }

        public Task<CoworkChannelResult<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken) => Task.FromResult(Load);

        public Task<CoworkChannelResult<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken)
        {
            Saved = instructions;
            return Task.FromResult(Save);
        }
    }

    private sealed class ScriptedProjects : IProjectListSource
    {
        public CoworkChannelResult<IReadOnlyList<ProjectListItem>> List { get; set; } = new(true, false, [], null, null);

        public Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(List);
    }

    private sealed class ScriptedEditor : IProjectEditorSource
    {
        public CoworkChannelResult<ProjectListItem> Read { get; set; } = new(false, false, null, "Error", null);

        public CoworkChannelResult<bool> Delete { get; set; } = new(true, false, true, null, null);

        public bool Deleted { get; private set; }

        public Task<CoworkChannelResult<ProjectListItem>> ReadAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Read);

        public Task<CoworkChannelResult<bool>> RenameAsync(string id, string name, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken)
        {
            Deleted = true;
            return Task.FromResult(Delete);
        }
    }

    private sealed class ScriptedMemory : IMemoryListSource
    {
        public CoworkChannelResult<IReadOnlyList<MemoryListItem>> List { get; set; } = new(true, false, [], null, null);

        public Task<CoworkChannelResult<IReadOnlyList<MemoryListItem>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(List);

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }

    private sealed class ScriptedSettings : IMemorySettingsSource
    {
        public CoworkChannelResult<MemorySettingsSnapshot> Load { get; set; } = new(true, false, new MemorySettingsSnapshot(false, false, false), null, null);

        public Task<CoworkChannelResult<MemorySettingsSnapshot>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Load);

        public Task<CoworkChannelResult<bool>> SaveAsync(MemorySettingsSnapshot snapshot, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }
}
