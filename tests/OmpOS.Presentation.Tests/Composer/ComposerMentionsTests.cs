using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Mentions;
using OmpOS.Presentation.Tests.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Composer;

/// <summary>The mentions of the composer view model: the picker opens from the draft and the caret, its keys attach instead of sending, and a send carries the attached items.</summary>
public sealed class ComposerMentionsTests : IDisposable
{
    private readonly FakeMentionWorld _world = new();
    private readonly List<string> _sent = [];
    private readonly List<(string Text, IReadOnlyList<MentionAttachment> Attachments)> _sentWithMentions = [];
    private readonly MentionComposerModel _mentions;
    private readonly ComposerViewModel _composer;

    /// <summary>Creates a composer over a world with one file and a mention model, with no mention sender.</summary>
    public ComposerMentionsTests()
    {
        _world.AddFile("src/App.cs", "class App;");
        _mentions = new MentionComposerModel([new FileMentionSource(_world)]);
        _composer = new ComposerViewModel(
            text =>
            {
                _sent.Add(text);
                return Task.FromResult(true);
            },
            static () => Task.CompletedTask)
        {
            Mentions = _mentions,
        };
    }

    /// <inheritdoc />
    public void Dispose() => _mentions.Dispose();

    private async Task TypeAsync(string text)
    {
        _composer.Draft = text;
        _composer.CaretIndex = text.Length;
        await _mentions.Picker.WaitSettledAsync().ConfigureAwait(true);
    }

    /// <summary>Typing an at sign with the caret after it opens the picker; moving the caret out of the token closes it.</summary>
    [Fact]
    public async Task TheDraftAndTheCaretOpenAndCloseThePicker()
    {
        await TypeAsync("see @App").ConfigureAwait(true);

        _composer.IsMentionPickerOpen.ShouldBeTrue();
        _mentions.Picker.Rows.Single().Candidate.Id.ShouldBe("src/App.cs");

        _composer.CaretIndex = 3;

        _composer.IsMentionPickerOpen.ShouldBeFalse();
    }

    /// <summary>Enter with a row selected attaches it and sends nothing; the draft holds the mention and the caret sits after it.</summary>
    [Fact]
    public async Task EnterAttachesInsteadOfSending()
    {
        await TypeAsync("see @App").ConfigureAwait(true);

        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);

        _sent.ShouldBeEmpty();
        _composer.Draft.ShouldBe("see @src/App.cs ");
        _composer.CaretIndex.ShouldBe(16);
        _mentions.Chips.Count.ShouldBe(1);
        _composer.IsMentionPickerOpen.ShouldBeFalse();
    }

    /// <summary>Escape closes the picker and keeps the draft; the next Escape is the ordinary one, which stops nothing while nothing streams.</summary>
    [Fact]
    public async Task EscapeClosesThePickerAndKeepsTheDraft()
    {
        await TypeAsync("see @App").ConfigureAwait(true);

        (await _composer.HandleMentionPickerKeyAsync("Escape").ConfigureAwait(true)).ShouldBeTrue();

        _composer.IsMentionPickerOpen.ShouldBeFalse();
        _composer.Draft.ShouldBe("see @App");
        (await _composer.HandleMentionPickerKeyAsync("Escape").ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Enter with the picker open but nothing listed is not taken by the picker, so the draft is sent as written.</summary>
    [Fact]
    public async Task EnterWithNothingListedSendsTheDraft()
    {
        await TypeAsync("see @zzzz").ConfigureAwait(true);
        _mentions.Picker.Rows.ShouldBeEmpty();

        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);

        _sent.ShouldBe(["see @zzzz"]);
    }

    /// <summary>The arrow keys move the highlight; keys the picker does not own are not taken; with the picker closed nothing is taken.</summary>
    [Fact]
    public async Task ArrowKeysMoveTheHighlightAndOtherKeysPassThrough()
    {
        _world.AddFile("src/Apple.cs");
        await TypeAsync("@App").ConfigureAwait(true);

        (await _composer.HandleMentionPickerKeyAsync("ArrowDown").ConfigureAwait(true)).ShouldBeTrue();
        _mentions.Picker.SelectedIndex.ShouldBe(1);
        (await _composer.HandleMentionPickerKeyAsync("ArrowUp").ConfigureAwait(true)).ShouldBeTrue();
        _mentions.Picker.SelectedIndex.ShouldBe(0);
        (await _composer.HandleMentionPickerKeyAsync("a").ConfigureAwait(true)).ShouldBeFalse();

        _composer.Draft = string.Empty;
        (await _composer.HandleMentionPickerKeyAsync("ArrowDown").ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A send with attached items hands the sender the text and the content read now; an accepted send clears the draft and the chips.</summary>
    [Fact]
    public async Task ASendCarriesTheAttachedItems()
    {
        _composer.MentionSender = (text, attachments) =>
        {
            _sentWithMentions.Add((text, attachments));
            return Task.FromResult(true);
        };
        await TypeAsync("see @App").ConfigureAwait(true);
        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);
        _world.Files["src/App.cs"] = "class App; // edited after attaching";
        _composer.Draft += "please";

        await _composer.SendAsync().ConfigureAwait(true);

        _sent.ShouldBeEmpty();
        var sent = _sentWithMentions.ShouldHaveSingleItem();
        sent.Text.ShouldBe("see @src/App.cs please");
        sent.Attachments.Single().Content.Text.ShouldBe("class App; // edited after attaching");
        _composer.Draft.ShouldBeEmpty();
        _mentions.Chips.ShouldBeEmpty();
    }

    /// <summary>Without a mention sender the text is sent alone through the ordinary callback and the chips are cleared.</summary>
    [Fact]
    public async Task WithoutASenderTheTextIsSentAlone()
    {
        await TypeAsync("see @App").ConfigureAwait(true);
        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);

        await _composer.SendAsync().ConfigureAwait(true);

        _sent.ShouldBe(["see @src/App.cs "]);
        _mentions.Chips.ShouldBeEmpty();
    }

    /// <summary>A refused send keeps the draft and the chips.</summary>
    [Fact]
    public async Task ARefusedSendKeepsTheDraftAndTheChips()
    {
        _composer.MentionSender = (_, _) => Task.FromResult(false);
        await TypeAsync("see @App").ConfigureAwait(true);
        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);

        await _composer.SendAsync().ConfigureAwait(true);

        _composer.Draft.ShouldBe("see @src/App.cs ");
        _mentions.Chips.Count.ShouldBe(1);
    }

    /// <summary>An attached item that cannot be read at send time stops the send: nothing is sent, the item is dropped, and the draft stays for another try.</summary>
    [Fact]
    public async Task AnUnreadableItemStopsTheSend()
    {
        _composer.MentionSender = (text, attachments) =>
        {
            _sentWithMentions.Add((text, attachments));
            return Task.FromResult(true);
        };
        await TypeAsync("see @App").ConfigureAwait(true);
        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);
        _world.Files.Remove("src/App.cs");

        await _composer.SendAsync().ConfigureAwait(true);

        _sentWithMentions.ShouldBeEmpty();
        _sent.ShouldBeEmpty();
        _composer.Draft.ShouldBe("see @src/App.cs ");
        _mentions.Chips.ShouldBeEmpty();
        _mentions.Notice.ShouldNotBeNull();
    }

    /// <summary>Removing a chip through the composer removes its text from the draft and moves the caret to where it was.</summary>
    [Fact]
    public async Task RemovingAChipRemovesItsText()
    {
        await TypeAsync("see @App").ConfigureAwait(true);
        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);
        _composer.Draft += "now";

        _composer.RemoveMention(_mentions.Chips.Single());

        _composer.Draft.ShouldBe("see now");
        _composer.CaretIndex.ShouldBe(4);
    }

    /// <summary>The ring's pending estimate follows the attached items and drops when they go.</summary>
    [Fact]
    public async Task TheRingEstimateFollowsTheChips()
    {
        var ring = new ContextRingModel();
        ring.ReportUsage(100, 1000);
        _composer.ContextRing = ring;

        await TypeAsync("see @App").ConfigureAwait(true);
        await _composer.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);

        ring.PendingTokens.ShouldBe(3);
        ring.ProjectedPercent.ShouldBe(10);

        _composer.Draft = "no mentions left";

        ring.PendingTokens.ShouldBe(0);
    }

    /// <summary>The caret is clamped to the draft, also when the draft shrinks.</summary>
    [Fact]
    public void TheCaretIsClampedToTheDraft()
    {
        _composer.Draft = "hello";
        _composer.CaretIndex = 99;
        _composer.CaretIndex.ShouldBe(5);

        _composer.Draft = "hi";

        _composer.CaretIndex.ShouldBe(2);
    }

    /// <summary>With no mention model nothing about the draft, the keys or the send changes.</summary>
    [Fact]
    public async Task WithoutMentionsNothingChanges()
    {
        var sent = new List<string>();
        var plain = new ComposerViewModel(text => { sent.Add(text); return Task.FromResult(true); }, static () => Task.CompletedTask) { Draft = "@App" };

        plain.CaretIndex = 4;
        plain.IsMentionPickerOpen.ShouldBeFalse();
        (await plain.HandleMentionPickerKeyAsync("Enter").ConfigureAwait(true)).ShouldBeFalse();
        await plain.HandleKeyAsync("Enter", shift: false).ConfigureAwait(true);

        sent.ShouldBe(["@App"]);
    }
}
