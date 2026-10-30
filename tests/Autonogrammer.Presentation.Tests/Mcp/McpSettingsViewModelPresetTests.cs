using Autonogrammer.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mcp;

/// <summary>The add-from-preset list of the MCP page: what each row says before it is added, what the button does, and how an added or refused preset shows.</summary>
public sealed class McpSettingsViewModelPresetTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static McpPresetChoice Remote() => new(
        "linear", "Linear", "Issues and projects in Linear.", McpTemplateChoice.Http, "https://linear.app/docs/mcp", McpCopy.AuthOAuth, "Read-only access is available.");

    private static McpPresetChoice Local() => new(
        "fetch", "Fetch", "Fetch a web page.", McpTemplateChoice.Stdio, "https://example.com/fetch", McpCopy.AuthNone, null);

    private static async Task<(McpSettingsViewModel Model, ScriptedMcpSource Source)> LoadedAsync(ScriptedMcpSource source)
    {
        source.PresetList.AddRange([Remote(), Local()]);
        var model = new McpSettingsViewModel(source);
        await model.LoadAsync(Token).ConfigureAwait(true);
        return (model, source);
    }

    /// <summary>Before the page has loaded no preset is offered; afterwards every preset is a row with its transport, sign-in sentence, vendor link and note.</summary>
    [Fact]
    public async Task TheListAppearsAfterTheLoadAndEachRowSaysHowItSignsIn()
    {
        var source = new ScriptedMcpSource();
        source.PresetList.AddRange([Remote(), Local()]);
        var model = new McpSettingsViewModel(source);

        model.ShowPresets.ShouldBeFalse();
        model.Presets.ShouldBeEmpty();

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.ShowPresets.ShouldBeTrue();
        var linear = model.Presets[0];
        linear.DisplayName.ShouldBe("Linear");
        linear.TransportLabel.ShouldBe(McpCopy.PresetRemote);
        linear.AuthNote.ShouldBe(McpCopy.AuthOAuth, "the page says it signs in through OAuth and holds no credential");
        linear.DocumentationText.ShouldBe("Vendor docs: https://linear.app/docs/mcp");
        linear.HasNote.ShouldBeTrue();
        linear.NoteText.ShouldBe("Read-only access is available.");
        linear.CanAdd.ShouldBeTrue();
        linear.AddName.ShouldBe("Add Linear");
        var fetch = model.Presets[1];
        fetch.TransportLabel.ShouldBe(McpCopy.PresetLocal);
        fetch.HasNote.ShouldBeFalse();
        fetch.NoteText.ShouldBeEmpty();
    }

    /// <summary>A source with no preset hides the section.</summary>
    [Fact]
    public async Task ASourceWithoutPresetsHidesTheSection()
    {
        var model = new McpSettingsViewModel(new ScriptedMcpSource());

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.ShowPresets.ShouldBeFalse();
    }

    /// <summary>The button adds the preset under its own name, switched off, says so and says how to sign in; the row then reads Added and cannot be added again.</summary>
    [Fact]
    public async Task AddingAPresetWritesItSwitchedOffAndTheRowReadsAdded()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource()).ConfigureAwait(true);

        await ((McpActionCommand)model.Presets[0].AddCommand).ExecuteAsync().ConfigureAwait(true);

        source.Calls.ShouldBe(["load", "preset linear"]);
        model.ActionFailure.ShouldBeEmpty();
        model.ActionResult.ShouldBe("Added linear to .autonogrammer/mcp.json. It is switched off until you switch it on. " + McpCopy.AuthOAuth);
        var row = model.Rows.Single();
        row.Name.ShouldBe("linear");
        row.IsEnabled.ShouldBeFalse();
        row.StatusText.ShouldBe("Off");
        model.Presets[0].IsAdded.ShouldBeTrue();
        model.Presets[0].CanAdd.ShouldBeFalse();
        model.Presets[0].AddLabel.ShouldBe(McpCopy.PresetAddedLabel);
        model.Presets[0].AddName.ShouldBe("Added Linear");
        model.Presets[1].CanAdd.ShouldBeTrue("only the added preset is locked");
    }

    /// <summary>A server that already has the preset's name is refused by the source; the page shows the reason and writes nothing more.</summary>
    [Fact]
    public async Task AServerWithThePresetsNameIsRefusedAndTheReasonIsShown()
    {
        var existing = McpEntries.Ready("linear");
        var (model, source) = await LoadedAsync(new ScriptedMcpSource(existing)).ConfigureAwait(true);
        model.Presets[0].IsAdded.ShouldBeTrue("the page already knows the name is taken");

        await model.AddPresetAsync("linear", Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("A server named 'linear' is already configured in ~/.cursor/mcp.json.");
        model.ActionResult.ShouldBeEmpty();
        model.Rows.Single().Entry.ShouldBe(existing);
        source.Calls.ShouldBe(["load", "preset linear"]);
    }

    /// <summary>With no workspace open the owned file has no home: the buttons are disabled with the template explanation visible.</summary>
    [Fact]
    public async Task WithoutAWorkspaceNoPresetCanBeAdded()
    {
        var source = new ScriptedMcpSource { OwnedOrigin = null };
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);

        model.TemplatesNeedWorkspace.ShouldBeTrue();
        model.Presets.ShouldAllBe(row => !row.CanAdd);
    }

    /// <summary>A write failure is reported and the list is not changed.</summary>
    [Fact]
    public async Task AFailedWriteIsReportedAndNothingIsAdded()
    {
        var source = new ScriptedMcpSource { WriteFailure = "The file .autonogrammer/mcp.json could not be written." };
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);

        await model.AddPresetAsync("fetch", Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("The file .autonogrammer/mcp.json could not be written.");
        model.Rows.ShouldBeEmpty();
        model.Presets.ShouldAllBe(row => !row.IsAdded);
    }

    /// <summary>An id the source does not offer is refused.</summary>
    [Fact]
    public async Task AnUnknownPresetIsRefused()
    {
        var (model, _) = await LoadedAsync(new ScriptedMcpSource()).ConfigureAwait(true);

        await model.AddPresetAsync("nope", Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("There is no MCP preset named 'nope'.");
        model.Rows.ShouldBeEmpty();
    }

    /// <summary>A null id is a programming error.</summary>
    [Fact]
    public async Task ANullIdThrows()
    {
        var (model, _) = await LoadedAsync(new ScriptedMcpSource()).ConfigureAwait(true);

        await Should.ThrowAsync<ArgumentNullException>(() => model.AddPresetAsync(null!, Token)).ConfigureAwait(true);
    }
}
