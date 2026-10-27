using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// The slash palette of <see cref="ComposerViewModel"/>: it opens while the first token of the draft is a slash and a name, follows the draft,
/// takes its keys, completes a name without sending, and leaves the send and stop rules as they were (SLASH-1).
/// </summary>
public sealed class ComposerSlashPaletteTests
{
    private static readonly SlashEngineCommand[] Advertised =
    [
        new("security", "Plan, run, inspect scans", "<plan|scan>"),
        new("model", "Show current model selection", null),
        new("fast", "Toggle fast mode", "[on|off]"),
        new("slow", "Toggle slow mode", "[on|off]"),
    ];

    private sealed record Rig(ComposerViewModel Composer, SlashPaletteModel Palette, List<string> Sent, int[] Stops);

    private static Rig Create(bool refuseSend = false)
    {
        var sent = new List<string>();
        var stops = new int[1];
        var composer = new ComposerViewModel(
            text =>
            {
                sent.Add(text);
                return Task.FromResult(!refuseSend);
            },
            () =>
            {
                stops[0]++;
                return Task.CompletedTask;
            });
        var palette = new SlashPaletteModel(new SlashCommandRegistry([]));
        palette.SetEngineCommands(Advertised);
        composer.SlashPalette = palette;
        return new Rig(composer, palette, sent, stops);
    }

    /// <summary>Typing a slash opens the palette with every command, more text filters it, and clearing the draft closes it.</summary>
    [Fact]
    public void TheDraftOpensFiltersAndClosesThePalette()
    {
        var rig = Create();

        rig.Composer.Draft = "/";
        rig.Palette.IsOpen.ShouldBeTrue();
        rig.Palette.Entries.Select(entry => entry.Name).ShouldBe(["fast", "model", "security", "slow"]);

        rig.Composer.Draft = "/s";
        rig.Palette.Entries.Select(entry => entry.Name).ShouldBe(["security", "slow"]);

        rig.Composer.Draft = string.Empty;
        rig.Palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>A slash in the middle of the text, or after a space, never opens the palette.</summary>
    [Theory]
    [InlineData("fix /model")]
    [InlineData("hello")]
    [InlineData("/model now")]
    [InlineData("/model ")]
    public void ADraftThatDoesNotStartWithACommandNameDoesNotOpenIt(string draft)
    {
        var rig = Create();

        rig.Composer.Draft = draft;

        rig.Palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>Assigning a palette to a composer whose draft already starts with a slash opens it at once.</summary>
    [Fact]
    public void AssigningAPaletteReportsTheCurrentDraft()
    {
        var composer = new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask) { Draft = "/mo" };
        var palette = new SlashPaletteModel(new SlashCommandRegistry([]));
        palette.SetEngineCommands(Advertised);

        composer.SlashPalette = palette;

        palette.IsOpen.ShouldBeTrue();
        palette.Entries.ShouldHaveSingleItem().Name.ShouldBe("model");
    }

    /// <summary>With no palette (a terminal session) the draft, the keys and the send rules behave exactly as before.</summary>
    [Fact]
    public async Task WithoutAPaletteNothingChanges()
    {
        var sent = new List<string>();
        var composer = new ComposerViewModel(text => { sent.Add(text); return Task.FromResult(true); }, static () => Task.CompletedTask) { Draft = "/model" };

        composer.HandleSlashPaletteKey("Enter").ShouldBeFalse();
        composer.AcceptSlashEntry().ShouldBeFalse();
        await composer.HandleKeyAsync("Enter", shift: false);

        sent.ShouldBe(["/model"]);
    }

    /// <summary>Down and Up move the highlight, wrapping, and the draft stays as typed.</summary>
    [Fact]
    public void ArrowKeysMoveTheHighlightWithoutChangingTheDraft()
    {
        var rig = Create();
        rig.Composer.Draft = "/";

        rig.Composer.HandleSlashPaletteKey("ArrowDown").ShouldBeTrue();
        rig.Palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("model");
        rig.Composer.HandleSlashPaletteKey("ArrowUp").ShouldBeTrue();
        rig.Composer.HandleSlashPaletteKey("ArrowUp").ShouldBeTrue();
        rig.Palette.SelectedEntry.ShouldNotBeNull().Name.ShouldBe("slow");

        rig.Composer.Draft.ShouldBe("/");
    }

    /// <summary>Enter and Tab accept the highlighted command: the draft becomes the name and a space, the palette closes, and nothing is sent.</summary>
    [Theory]
    [InlineData("Enter")]
    [InlineData("Tab")]
    public async Task EnterAndTabCompleteTheNameAndDoNotSend(string key)
    {
        var rig = Create();
        rig.Composer.Draft = "/se";

        var handled = rig.Composer.HandleSlashPaletteKey(key);

        handled.ShouldBeTrue();
        rig.Composer.Draft.ShouldBe("/security ");
        rig.Palette.IsOpen.ShouldBeFalse();
        rig.Sent.ShouldBeEmpty();

        // The next Enter is the ordinary send: the completed text goes to the engine, which runs the command.
        await rig.Composer.HandleKeyAsync("Enter", shift: false);
        rig.Sent.ShouldBe(["/security "]);
    }

    /// <summary>HandleKeyAsync routes Enter to the palette first while it is open, so Enter accepts instead of sending.</summary>
    [Fact]
    public async Task HandleKeyAsyncGivesTheOpenPaletteTheFirstKey()
    {
        var rig = Create();
        rig.Composer.Draft = "/mo";

        await rig.Composer.HandleKeyAsync("Enter", shift: false);

        rig.Sent.ShouldBeEmpty();
        rig.Composer.Draft.ShouldBe("/model ");
    }

    /// <summary>Escape closes the palette first and does not stop a streaming response; the next Escape stops it.</summary>
    [Fact]
    public async Task EscapeClosesThePaletteBeforeItStopsAResponse()
    {
        var rig = Create();
        rig.Composer.IsStreaming = true;
        rig.Composer.Draft = "/mo";

        await rig.Composer.HandleKeyAsync("Escape", shift: false);

        rig.Palette.IsOpen.ShouldBeFalse();
        rig.Stops[0].ShouldBe(0);
        rig.Composer.Draft.ShouldBe("/mo");

        await rig.Composer.HandleKeyAsync("Escape", shift: false);

        rig.Stops[0].ShouldBe(1);
    }

    /// <summary>A command typed by hand that nobody lists is not handled by the palette, so Enter sends it exactly as written.</summary>
    [Fact]
    public async Task AnUnknownCommandTypedByHandIsSentAsWritten()
    {
        var rig = Create();
        rig.Composer.Draft = "/nonesuch";
        rig.Palette.IsOpen.ShouldBeTrue();
        rig.Palette.Entries.ShouldBeEmpty();

        rig.Composer.HandleSlashPaletteKey("Enter").ShouldBeFalse();
        await rig.Composer.HandleKeyAsync("Enter", shift: false);

        rig.Sent.ShouldBe(["/nonesuch"]);
    }

    /// <summary>With the palette closed (plain text) Enter, Tab and the arrows are not the palette's: Enter sends and the other keys are left to the text control.</summary>
    [Fact]
    public async Task ClosedPaletteTakesNoKeys()
    {
        var rig = Create();
        rig.Composer.Draft = "hello";

        rig.Composer.HandleSlashPaletteKey("ArrowDown").ShouldBeFalse();
        rig.Composer.HandleSlashPaletteKey("Tab").ShouldBeFalse();
        rig.Composer.HandleSlashPaletteKey("Escape").ShouldBeFalse();
        await rig.Composer.HandleKeyAsync("Enter", shift: false);

        rig.Sent.ShouldBe(["hello"]);
    }

    /// <summary>Shift+Enter is never the palette's: it is left to the text control, which inserts a newline.</summary>
    [Fact]
    public async Task ShiftEnterIsLeftToTheTextControl()
    {
        var rig = Create();
        rig.Composer.Draft = "/mo";

        await rig.Composer.HandleKeyAsync("Enter", shift: true);

        rig.Composer.Draft.ShouldBe("/mo");
        rig.Sent.ShouldBeEmpty();
        rig.Palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>Accepting with a click-style call completes the highlighted row; with no palette or no row it changes nothing.</summary>
    [Fact]
    public void AcceptSlashEntryCompletesTheHighlightedRow()
    {
        var rig = Create();
        rig.Composer.Draft = "/";
        rig.Palette.Select(3);

        rig.Composer.AcceptSlashEntry().ShouldBeTrue();

        rig.Composer.Draft.ShouldBe("/slow ");
        rig.Composer.AcceptSlashEntry().ShouldBeFalse();
        rig.Composer.Draft.ShouldBe("/slow ");
    }

    /// <summary>An accepted send clears the draft and so closes the palette.</summary>
    [Fact]
    public async Task SendingClearsTheDraftAndClosesThePalette()
    {
        var rig = Create();
        rig.Composer.Draft = "/nonesuch";

        await rig.Composer.SendAsync();

        rig.Composer.Draft.ShouldBeEmpty();
        rig.Palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>A null key is refused.</summary>
    [Fact]
    public void ANullKeyIsRefused() =>
        Should.Throw<ArgumentNullException>(() => Create().Composer.HandleSlashPaletteKey(null!));
}
