using System.Text.Json;
using OmpOS.Native.Settings;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Ignore;
using OmpOS.Workspace.Ignore;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The Ignore source: the values in settings.json, what a write keeps and refuses, the ignore files found and the matcher options a value stands for.</summary>
public sealed class IgnoreSettingsSourceTests : IDisposable
{
    private readonly HeadTempDirectory _state = new();
    private readonly HeadTempDirectory _workspace = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _state.Dispose();
        _workspace.Dispose();
    }

    private IgnoreSettingsSource Source(string stateName = "state") => new(new FileSettingsStore(_state.PathOf(stateName)), _workspace.Root);

    /// <summary>With no settings file the values are the defaults and nothing is created.</summary>
    [Fact]
    public async Task NoFileGivesTheDefaults()
    {
        var values = await Source().LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.ShouldBe(IgnoreSettingsValues.Defaults);
        File.Exists(_state.PathOf("state/config/settings.json")).ShouldBeFalse();
    }

    /// <summary>Saved values read back, in order, and are stored as ignore.globalPatterns, ignore.useDefaultList and ignore.hierarchical.</summary>
    [Fact]
    public async Task ValuesRoundTripAndUseTheDocumentedMembers()
    {
        var token = TestContext.Current.CancellationToken;
        var source = Source();
        var values = new IgnoreSettingsValues(["build/", "!keep.png", "*.log"], UseDefaultList: false, HierarchicalIgnoreFiles: true);

        await source.SaveAsync(values, token).ConfigureAwait(true);

        (await Source().LoadAsync(token).ConfigureAwait(true)).ShouldBe(values);
        using var json = JsonDocument.Parse(File.ReadAllText(_state.PathOf("state/config/settings.json")));
        var ignore = json.RootElement.GetProperty("ignore");
        ignore.GetProperty("globalPatterns").EnumerateArray().Select(static item => item.GetString()).ShouldBe(["build/", "!keep.png", "*.log"]);
        ignore.GetProperty("useDefaultList").GetBoolean().ShouldBeFalse();
        ignore.GetProperty("hierarchical").GetBoolean().ShouldBeTrue();
    }

    /// <summary>A write keeps every member this build does not know, in the document and inside the ignore object, and the other settings.</summary>
    [Fact]
    public async Task AWriteKeepsUnknownMembersAndOtherSettings()
    {
        var token = TestContext.Current.CancellationToken;
        _ = _state.Write(
            "state/config/settings.json",
            "{\"schemaVersion\":1,\"theme\":\"dark\",\"futureThing\":{\"a\":1},\"ignore\":{\"futureFlag\":true,\"globalPatterns\":[\"old/\"]}}");
        var source = Source();

        await source.SaveAsync(new IgnoreSettingsValues(["new/"], true, false), token).ConfigureAwait(true);

        using var json = JsonDocument.Parse(File.ReadAllText(_state.PathOf("state/config/settings.json")));
        json.RootElement.GetProperty("futureThing").GetProperty("a").GetInt32().ShouldBe(1);
        json.RootElement.GetProperty("theme").GetString().ShouldBe("dark", Case.Sensitive);
        json.RootElement.GetProperty("ignore").GetProperty("futureFlag").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("ignore").GetProperty("globalPatterns")[0].GetString().ShouldBe("new/", Case.Sensitive);
    }

    /// <summary>A file from a newer major schema fails the read and the write with the same message and is left untouched.</summary>
    [Fact]
    public async Task ANewerSchemaIsAnErrorAndIsLeftUntouched()
    {
        var token = TestContext.Current.CancellationToken;
        const string Newer = "{\"schemaVersion\":2,\"ignore\":{\"globalPatterns\":[\"x\"]}}";
        var path = _state.Write("state/config/settings.json", Newer);
        var source = Source();

        var read = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(token)).ConfigureAwait(true);
        var write = await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync(IgnoreSettingsValues.Defaults, token)).ConfigureAwait(true);

        read.Message.ShouldContain("newer OmpOS", Case.Sensitive);
        write.Message.ShouldBe(read.Message, Case.Sensitive);
        File.ReadAllText(path).ShouldBe(Newer);
    }

    /// <summary>A stored pattern the page would refuse (blank) makes the file an error state that is reported and never overwritten.</summary>
    [Fact]
    public async Task AnUnusableStoredPatternIsReportedAndNeverOverwritten()
    {
        var token = TestContext.Current.CancellationToken;
        const string Bad = "{\"ignore\":{\"globalPatterns\":[\"ok\",\"   \"]}}";
        var path = _state.Write("state/config/settings.json", Bad);
        var source = Source();

        var read = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync(IgnoreSettingsValues.Defaults, token)).ConfigureAwait(true);

        read.Message.ShouldContain("ignore.globalPatterns", Case.Sensitive);
        File.ReadAllText(path).ShouldBe(Bad);
    }

    /// <summary>A list over the limits is refused before anything is written.</summary>
    [Fact]
    public async Task ANeedlesslyLargeListIsRefusedBeforeAWrite()
    {
        var source = Source();
        var tooMany = new IgnoreSettingsValues([.. Enumerable.Range(0, IgnoreSettingsLimits.MaxPatterns + 1).Select(static i => "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))], true, false);

        _ = await Should.ThrowAsync<SettingsSourceException>(() => source.SaveAsync(tooMany, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        File.Exists(_state.PathOf("state/config/settings.json")).ShouldBeFalse();
    }

    /// <summary>Saving ignore settings leaves the other values of the file as they were.</summary>
    [Fact]
    public async Task SavingIgnoreLeavesTheGeneralSettingsAlone()
    {
        var store = new FileSettingsStore(_state.PathOf("state"));
        store.SetTheme(OmpOS.Presentation.ThemePreference.Dark);

        await new IgnoreSettingsSource(store, _workspace.Root).SaveAsync(new IgnoreSettingsValues(["a"], true, true), TestContext.Current.CancellationToken).ConfigureAwait(true);

        store.Read().Theme.ShouldBe(OmpOS.Presentation.ThemePreference.Dark);
    }

    /// <summary>The ignore files at the workspace root are listed with their rule counts and what was skipped; absent files are not listed.</summary>
    [Fact]
    public async Task ListsTheIgnoreFilesThatExistWithTheirRuleCounts()
    {
        _ = _workspace.Write(".cursorignore", "# a comment\nbuild/\n\n*.log\n");
        _ = _workspace.Write(".gitignore", "node_modules/\n");

        var files = await Source().FoundFilesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        files.Select(static file => file.Name).ShouldBe([".cursorignore", ".gitignore"]);
        files[0].RuleCount.ShouldBe(2);
        files[0].Problems.ShouldBeEmpty();
        files[1].RuleCount.ShouldBe(1);
    }

    /// <summary>A rule the matcher skips is reported on its file with the line number.</summary>
    [Fact]
    public async Task AFileWithASkippedRuleReportsTheProblem()
    {
        _ = _workspace.Write(".ompos/ignore", "ok/\n" + new string('a', 2000) + "\n");

        var files = await Source().FoundFilesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var file = files.ShouldHaveSingleItem();
        file.Name.ShouldBe(".ompos/ignore", Case.Sensitive);
        file.RuleCount.ShouldBe(1);
        file.Problems.ShouldHaveSingleItem().ShouldStartWith("Line 2:", Case.Sensitive);
    }

    /// <summary>A workspace with none of the files lists nothing.</summary>
    [Fact]
    public async Task NoIgnoreFilesListsNothing()
    {
        (await Source().FoundFilesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>The built-in list is the matcher's own list.</summary>
    [Fact]
    public void TheBuiltInListIsTheMatchersList()
    {
        Source().DefaultPatterns.ShouldBe(IgnoreDefaults.Patterns);
    }

    /// <summary>The options a value stands for carry the global patterns and the built-in switch, and keep every other option at its default.</summary>
    [Fact]
    public void ToIgnoreOptionsCarriesThePatternsAndTheSwitch()
    {
        var options = IgnoreSettingsSource.ToIgnoreOptions(new IgnoreSettingsValues(["a/", "!b"], UseDefaultList: false, HierarchicalIgnoreFiles: true));

        options.GlobalPatterns.ShouldBe(["a/", "!b"]);
        options.UseDefaultList.ShouldBeFalse();
        options.RespectGitIgnore.ShouldBeTrue();
        options.IgnoreFileNames.ShouldBe(new IgnoreOptions().IgnoreFileNames);
    }
}
