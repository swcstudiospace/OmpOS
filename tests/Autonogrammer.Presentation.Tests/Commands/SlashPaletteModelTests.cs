using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands;

/// <summary>Slash palette model: opening on a slash, listing, suggestions, disabled reasons and selection (VX-02).</summary>
public sealed class SlashPaletteModelTests
{
    private static SlashPaletteModel Palette() => new(new SlashCommandRegistry(
    [
        Command("resume"),
        Command("rewind"),
        Command("clear"),
        Command("fork", "Needs a completed prompt; run one first."),
    ]));

    /// <summary>A bare slash opens the palette and lists every registered command in ordinal name order.</summary>
    [Fact]
    public void EmptyQueryListsEveryCommand()
    {
        var palette = Palette();

        palette.UpdateDraft("/");

        palette.IsOpen.ShouldBeTrue();
        palette.Query.ShouldBeEmpty();
        palette.Entries.Select(entry => entry.Name).ShouldBe(["clear", "fork", "resume", "rewind"]);
        palette.NoMatchMessage.ShouldBeNull();
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("clear");
    }

    /// <summary>A typed prefix narrows the list to the commands whose names start with it.</summary>
    [Fact]
    public void PrefixNarrowsTheList()
    {
        var palette = Palette();

        palette.UpdateDraft("/re");

        palette.Query.ShouldBe("re");
        palette.Entries.Select(entry => entry.Name).ShouldBe(["resume", "rewind"]);
    }

    /// <summary>A name that matches no command lists nothing, offers the close registered name, and says so.</summary>
    [Fact]
    public void UnknownQueryOffersASuggestion()
    {
        var palette = Palette();

        palette.UpdateDraft("/rewid");

        palette.IsOpen.ShouldBeTrue();
        palette.Entries.ShouldBeEmpty();
        palette.SelectedEntry.ShouldBeNull();
        palette.Suggestions.ShouldBe(["rewind"]);
        palette.NoMatchMessage.ShouldBe("No command matches '/rewid'. Did you mean /rewind?");
    }

    /// <summary>A name that matches nothing and is close to nothing says no command matches and offers no suggestion.</summary>
    [Fact]
    public void UnknownQueryWithNothingCloseOffersNoSuggestion()
    {
        var palette = Palette();

        palette.UpdateDraft("/zzzzzzzz");

        palette.Suggestions.ShouldBeEmpty();
        palette.NoMatchMessage.ShouldBe("No command matches '/zzzzzzzz'.");
    }

    /// <summary>A disabled command stays in the list with its stored reason, and accepting it shows that reason instead of completing.</summary>
    [Fact]
    public void DisabledCommandShowsItsReason()
    {
        var palette = Palette();
        palette.UpdateDraft("/fo");

        var entry = palette.SelectedEntry.ShouldNotBeNull();
        entry.Name.ShouldBe("fork");
        entry.IsEnabled.ShouldBeFalse();
        entry.DisabledReason.ShouldBe("Needs a completed prompt; run one first.");

        palette.Accept().ShouldBeNull();

        palette.Notice.ShouldBe("Needs a completed prompt; run one first.");
        palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>A slash opens the palette, and a backspace over the slash leaves an empty draft that closes it again.</summary>
    [Fact]
    public void SlashOpensAndBackspaceClosesThePalette()
    {
        var palette = Palette();
        palette.IsOpen.ShouldBeFalse();

        palette.UpdateDraft("/");
        palette.IsOpen.ShouldBeTrue();

        palette.UpdateDraft(string.Empty);

        palette.IsOpen.ShouldBeFalse();
        palette.Entries.ShouldBeEmpty();
        palette.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Draft text that is not a command name being typed closes the palette: plain text, a slash in the middle, or argument text after the name.</summary>
    [Theory]
    [InlineData("hello")]
    [InlineData("see /clear")]
    [InlineData("/clear now")]
    [InlineData("/clear\nnext line")]
    public void DraftThatIsNotACommandNameClosesThePalette(string draft)
    {
        var palette = Palette();
        palette.UpdateDraft("/c");
        palette.IsOpen.ShouldBeTrue();

        palette.UpdateDraft(draft);

        palette.IsOpen.ShouldBeFalse();
        palette.Query.ShouldBeEmpty();
    }

    /// <summary>Accepting an enabled command closes the palette and yields the draft that completes it, with a space for arguments.</summary>
    [Fact]
    public void AcceptingAnEnabledCommandCompletesTheDraftAndCloses()
    {
        var palette = Palette();
        palette.UpdateDraft("/res");

        palette.Accept().ShouldBe("/resume ");

        palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>Accepting with no row selected yields nothing.</summary>
    [Fact]
    public void AcceptingWithNoMatchYieldsNothing()
    {
        var palette = Palette();
        palette.UpdateDraft("/zzzzzzzz");

        palette.Accept().ShouldBeNull();

        palette.Notice.ShouldBeNull();
    }

    /// <summary>Selection moves by the given number of rows and wraps past either end.</summary>
    [Fact]
    public void SelectionMovesAndWraps()
    {
        var palette = Palette();
        palette.UpdateDraft("/");

        palette.MoveSelection(-1);
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("rewind");

        palette.MoveSelection(1);
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("clear");

        palette.MoveSelection(2);
        palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("resume");
    }

    /// <summary>Changing the draft clears a notice left by accepting a disabled command, and Dismiss closes the palette.</summary>
    [Fact]
    public void NoticeClearsOnTheNextDraftChangeAndDismissCloses()
    {
        var palette = Palette();
        palette.UpdateDraft("/fork");
        palette.Accept().ShouldBeNull();
        palette.Notice.ShouldNotBeNull();

        palette.UpdateDraft("/for");
        palette.Notice.ShouldBeNull();

        palette.Dismiss();
        palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>Opening the palette raises property changes for the state a view binds to.</summary>
    [Fact]
    public void OpeningRaisesPropertyChanges()
    {
        var palette = Palette();
        var changed = new List<string?>();
        palette.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        palette.UpdateDraft("/re");

        changed.ShouldContain(nameof(SlashPaletteModel.IsOpen), StringComparer.Ordinal);
        changed.ShouldContain(nameof(SlashPaletteModel.Query), StringComparer.Ordinal);
        changed.ShouldContain(nameof(SlashPaletteModel.Entries), StringComparer.Ordinal);
        changed.ShouldContain(nameof(SlashPaletteModel.SelectedEntry), StringComparer.Ordinal);
    }

    /// <summary>A null registry or a null draft is refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => new SlashPaletteModel(null!));
        Should.Throw<ArgumentNullException>(() => Palette().UpdateDraft(null!));
    }

    private static SlashCommandDescriptor Command(string name, string? disabledReason = null) => new()
    {
        Name = name,
        Description = $"Runs {name}.",
        Platforms = CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS,
        DisabledReason = disabledReason,
        HandlerId = $"test.{name}",
    };
}
