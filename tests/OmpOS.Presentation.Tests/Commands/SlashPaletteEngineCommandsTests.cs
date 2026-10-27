using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>The second source of the slash palette: the commands the engine advertises, merged with the OmpOS catalogue (SLASH-1).</summary>
public sealed class SlashPaletteEngineCommandsTests
{
    private static readonly SlashEngineCommand[] Advertised =
    [
        new("security", "Plan, run, inspect, import, and compare OMP-native security scans", "<plan|scan|status>"),
        new("model", "Show current model selection", null),
        new("fast", "Toggle fast mode", "[on|ultra|off|status]"),
        new("skill:review", "Review the change", "arguments"),
    ];

    private static SlashPaletteModel Palette(params SlashCommandDescriptor[] catalogue) => new(new SlashCommandRegistry(catalogue));

    private static SlashCommandDescriptor Catalogued(string name, string description = "Runs it.", params SlashCommandArgument[] arguments) => new()
    {
        Name = name,
        Description = description,
        Arguments = arguments,
        Platforms = CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS,
        HandlerId = $"test.{name}",
    };

    /// <summary>A bare slash lists the engine's commands and the catalogue's together in ordinal name order; each row has its description, input hint and source.</summary>
    [Fact]
    public void BothSourcesAreListedInNameOrderWithTheirSourceTag()
    {
        var palette = Palette(Catalogued("side", "Opens a side chat."));
        palette.SetEngineCommands(Advertised);

        palette.UpdateDraft("/");

        palette.Entries.Select(entry => entry.Name).ShouldBe(["fast", "model", "security", "side", "skill:review"]);
        var security = palette.Entries.Single(entry => string.Equals(entry.Name, "security", StringComparison.Ordinal));
        security.Source.ShouldBe(SlashPaletteSource.Omp);
        security.SourceLabel.ShouldBe("omp");
        security.Description.ShouldBe("Plan, run, inspect, import, and compare OMP-native security scans");
        security.InputHint.ShouldBe("<plan|scan|status>");
        security.DisplayName.ShouldBe("/security");
        var side = palette.Entries.Single(entry => string.Equals(entry.Name, "side", StringComparison.Ordinal));
        side.Source.ShouldBe(SlashPaletteSource.OmpOs);
        side.SourceLabel.ShouldBe("OmpOS");
        side.InputHint.ShouldBeNull();
        palette.Entries.Single(entry => string.Equals(entry.Name, "model", StringComparison.Ordinal)).HasInputHint.ShouldBeFalse();
    }

    /// <summary>When the engine and the catalogue name the same command, the engine's row is the only one and it is tagged omp.</summary>
    [Fact]
    public void TheEngineWinsAClashOfNames()
    {
        var palette = Palette(Catalogued("model", "The OmpOS model command."), Catalogued("side"));
        palette.SetEngineCommands([new SlashEngineCommand("model", "Show current model selection", null)]);

        palette.UpdateDraft("/m");

        var row = palette.Entries.ShouldHaveSingleItem();
        row.Source.ShouldBe(SlashPaletteSource.Omp);
        row.Description.ShouldBe("Show current model selection");
        palette.Announcement.ShouldBe("1 command");
    }

    /// <summary>The text after the slash filters both sources by name prefix, and the announcement counts the rows.</summary>
    [Fact]
    public void TheTypedNameFiltersBothSources()
    {
        var palette = Palette(Catalogued("side"), Catalogued("security-note"));
        palette.SetEngineCommands(Advertised);

        palette.UpdateDraft("/se");

        palette.Entries.Select(entry => entry.Name).ShouldBe(["security", "security-note"]);
        palette.Announcement.ShouldBe("2 commands");

        palette.UpdateDraft("/skill:");

        palette.Entries.Select(entry => entry.Name).ShouldBe(["skill:review"]);
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("skill:review");
    }

    /// <summary>With no engine command the palette lists the catalogue alone, as before the engine advertised anything.</summary>
    [Fact]
    public void WithoutEngineCommandsOnlyTheCatalogueIsListed()
    {
        var palette = Palette(Catalogued("side"));

        palette.UpdateDraft("/");

        palette.Entries.ShouldHaveSingleItem().Name.ShouldBe("side");
        palette.SetEngineCommands([]);
        palette.Entries.ShouldHaveSingleItem().Name.ShouldBe("side");
    }

    /// <summary>With neither source the open palette lists nothing, says so, and announces "No commands".</summary>
    [Fact]
    public void WithNeitherSourceThePaletteIsOpenAndEmpty()
    {
        var palette = Palette();

        palette.UpdateDraft("/x");

        palette.IsOpen.ShouldBeTrue();
        palette.Entries.ShouldBeEmpty();
        palette.SelectedEntry.ShouldBeNull();
        palette.Announcement.ShouldBe("No commands");
        palette.NoMatchMessage.ShouldBe("No command matches '/x'.");
        palette.Accept().ShouldBeNull();
    }

    /// <summary>A list the engine advertises while the palette is open reaches the open palette at once and keeps the selected command selected.</summary>
    [Fact]
    public void AnEngineUpdateWhileOpenRefreshesTheListAndKeepsTheSelection()
    {
        var palette = Palette(Catalogued("side"));
        palette.UpdateDraft("/");
        palette.Entries.Select(entry => entry.Name).ShouldBe(["side"]);

        palette.SetEngineCommands(Advertised);

        palette.Entries.Select(entry => entry.Name).ShouldBe(["fast", "model", "security", "side", "skill:review"]);

        // The row that was selected before the update ("side") is still listed, so it stays selected.
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("side");
        palette.MoveSelection(1);
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("skill:review");

        palette.SetEngineCommands([.. Advertised, new SlashEngineCommand("aaa", null, null)]);

        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("skill:review");
        palette.Entries.Count.ShouldBe(6);
    }

    /// <summary>An update replaces the list as a whole: a command the engine no longer advertises disappears.</summary>
    [Fact]
    public void AnEngineUpdateReplacesTheWholeList()
    {
        var palette = Palette();
        palette.SetEngineCommands(Advertised);
        palette.SetEngineCommands([new SlashEngineCommand("slow", "Toggle slow mode", "[on|off|status]")]);

        palette.UpdateDraft("/");

        palette.Entries.ShouldHaveSingleItem().Name.ShouldBe("slow");
    }

    /// <summary>A repeated name keeps its first entry, and a name that is blank or holds white space cannot be typed and is not listed.</summary>
    [Fact]
    public void RepeatedAndUntypableNamesAreNotListed()
    {
        var palette = Palette();
        palette.SetEngineCommands(
        [
            new SlashEngineCommand("fast", "first", null),
            new SlashEngineCommand("fast", "second", "[on]"),
            new SlashEngineCommand(string.Empty, "blank", null),
            new SlashEngineCommand("has space", "spaced", null),
            new SlashEngineCommand("tab\tname", "tabbed", null),
            new SlashEngineCommand("slow", "  Toggle slow mode  ", "   "),
        ]);

        palette.UpdateDraft("/");

        palette.Entries.Select(entry => entry.Name).ShouldBe(["fast", "slow"]);
        palette.Entries[0].Description.ShouldBe("first");
        palette.Entries[1].Description.ShouldBe("Toggle slow mode");
        palette.Entries[1].InputHint.ShouldBeNull();
    }

    /// <summary>A command without a description lists with an empty description that the row does not show.</summary>
    [Fact]
    public void ACommandWithoutADescriptionHasNoDescriptionRow()
    {
        var palette = Palette();
        palette.SetEngineCommands([new SlashEngineCommand("fast", null, "[on]")]);

        palette.UpdateDraft("/f");

        var row = palette.Entries.ShouldHaveSingleItem();
        row.Description.ShouldBeEmpty();
        row.HasDescription.ShouldBeFalse();
        row.HasInputHint.ShouldBeTrue();
        row.AutomationName.ShouldBe("/fast, [on], omp");
    }

    /// <summary>The input hint of a catalogue command lists its declared arguments: required ones in angle brackets, optional ones in square brackets.</summary>
    [Fact]
    public void ACatalogueCommandHintListsItsArguments()
    {
        var palette = Palette(Catalogued(
            "open",
            "Opens a file.",
            new SlashCommandArgument { Name = "file", IsRequired = true, Description = "The file." },
            new SlashCommandArgument { Name = "line", IsRequired = false, Description = "The line." }));

        palette.UpdateDraft("/op");

        palette.SelectedEntry.ShouldNotBeNull().InputHint.ShouldBe("<file> [line]");
    }

    /// <summary>Accepting an engine command only yields the text that completes the name; the palette closes and nothing else happens.</summary>
    [Fact]
    public void AcceptingAnEngineCommandYieldsTheCompletedNameAndCloses()
    {
        var palette = Palette();
        palette.SetEngineCommands(Advertised);
        palette.UpdateDraft("/sec");

        palette.Accept().ShouldBe("/security ");

        palette.IsOpen.ShouldBeFalse();
        palette.Entries.ShouldBeEmpty();
        palette.Announcement.ShouldBeNull();
    }

    /// <summary>Suggestions for a name nobody lists are drawn from both sources.</summary>
    [Fact]
    public void SuggestionsComeFromBothSources()
    {
        var palette = Palette(Catalogued("side"));
        palette.SetEngineCommands(Advertised);

        palette.UpdateDraft("/securty");

        palette.Entries.ShouldBeEmpty();
        palette.Suggestions.ShouldBe(["security"]);
        palette.NoMatchMessage.ShouldBe("No command matches '/securty'. Did you mean /security?");
        palette.UpdateDraft("/sdie");
        palette.Suggestions.ShouldBe(["side"]);
    }

    /// <summary>Escape (Dismiss) closes the palette and it stays closed, also when the engine advertises again, until the draft changes.</summary>
    [Fact]
    public void ADismissedPaletteStaysClosedUntilTheDraftChanges()
    {
        var palette = Palette();
        palette.SetEngineCommands(Advertised);
        palette.UpdateDraft("/fa");
        palette.IsOpen.ShouldBeTrue();

        palette.Dismiss();
        palette.SetEngineCommands([.. Advertised, new SlashEngineCommand("slow", null, null)]);
        palette.UpdateDraft("/fa");

        palette.IsOpen.ShouldBeFalse();

        palette.UpdateDraft("/fas");

        palette.IsOpen.ShouldBeTrue();
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("fast");
    }

    /// <summary>The palette is closed for a draft that is not a command name being typed: plain text, a slash after text, argument text after the name, a second line.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("hello /model")]
    [InlineData("/model now")]
    [InlineData("/model ")]
    [InlineData("/model\nnext")]
    [InlineData(" /model")]
    public void TheClosedStatesStayClosedWithEngineCommands(string draft)
    {
        var palette = Palette();
        palette.SetEngineCommands(Advertised);

        palette.UpdateDraft(draft);

        palette.IsOpen.ShouldBeFalse();
        palette.Entries.ShouldBeEmpty();
        palette.Announcement.ShouldBeNull();
    }

    /// <summary>Select moves the highlight to a listed row, as a click does, and ignores an index outside the list.</summary>
    [Fact]
    public void SelectMovesTheHighlightToAListedRow()
    {
        var palette = Palette();
        palette.SetEngineCommands(Advertised);
        palette.UpdateDraft("/");

        palette.Select(2);
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("security");

        palette.Select(99);
        palette.Select(-1);

        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("security");
    }

    /// <summary>Opening the palette with engine commands announces the count through a property change.</summary>
    [Fact]
    public void OpeningRaisesTheAnnouncementChange()
    {
        var palette = Palette();
        palette.SetEngineCommands(Advertised);
        var changed = new List<string?>();
        palette.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        palette.UpdateDraft("/");

        changed.ShouldContain(nameof(SlashPaletteModel.Announcement), StringComparer.Ordinal);
        palette.Announcement.ShouldBe("4 commands");
    }

    /// <summary>A null list or a null entry is refused.</summary>
    [Fact]
    public void NullEngineCommandsAreRefused()
    {
        var palette = Palette();

        Should.Throw<ArgumentNullException>(() => palette.SetEngineCommands(null!));
        Should.Throw<ArgumentNullException>(() => palette.SetEngineCommands([null!]));
    }
}
