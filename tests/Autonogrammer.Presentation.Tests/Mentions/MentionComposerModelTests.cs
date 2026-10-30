using Autonogrammer.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mentions;

/// <summary>The mention state of a composer: accepting replaces the token and adds a chip, removing and deleting drop it, sends read the content, the estimate follows the chips.</summary>
public sealed class MentionComposerModelTests : IDisposable
{
    private static readonly CancellationToken Token = TestContext.Current.CancellationToken;

    private readonly FakeMentionWorld _world = new();
    private readonly MentionComposerModel _model;

    /// <summary>Creates the model over a world with a file, a file whose name holds a space, an env file and a git source.</summary>
    public MentionComposerModelTests()
    {
        _world.AddFile("src/App.cs", "class App;");
        _world.AddFile("my notes.md", "notes");
        _world.AddFile(".env", "KEY=1");
        _model = new MentionComposerModel([new FileMentionSource(_world), new GitDiffMentionSource(MentionKind.GitDiffWorking, _world)]);
    }

    /// <inheritdoc />
    public void Dispose() => _model.Dispose();

    private async Task<MentionEdit?> TypeAndAcceptAsync(string draft, int caret, int row = 0)
    {
        _model.UpdateDraft(draft, caret);
        await _model.Picker.WaitSettledAsync().ConfigureAwait(true);
        _model.Picker.Select(row).ShouldBeTrue();
        return await _model.AcceptSelectedAsync(draft, Token).ConfigureAwait(true);
    }

    /// <summary>Accepting replaces the typed token with the canonical text, adds a chip and puts the caret after the text.</summary>
    [Fact]
    public async Task AcceptingReplacesTheTokenAndAddsAChip()
    {
        var edit = await TypeAndAcceptAsync("see @App", 8).ConfigureAwait(true);

        edit.ShouldNotBeNull();
        edit.Draft.ShouldBe("see @src/App.cs ");
        edit.Caret.ShouldBe(16);
        var chip = _model.Chips.ShouldHaveSingleItem();
        chip.Token.ShouldBe("@src/App.cs");
        chip.SizeText.ShouldBe("10 B");
        chip.RemoveName.ShouldBe("Remove src/App.cs");
        chip.AccessibleName.ShouldBe("file src/App.cs, 10 B");
    }

    /// <summary>A mention in the middle of the text keeps what follows it, and a name with spaces is written in quotes.</summary>
    [Fact]
    public async Task AMentionInTheMiddleKeepsTheRestAndQuotesNamesWithSpaces()
    {
        var edit = await TypeAndAcceptAsync("fix @notes please", 10).ConfigureAwait(true);

        edit.ShouldNotBeNull();
        edit.Draft.ShouldBe("fix @\"my notes.md\" please");
        edit.Caret.ShouldBe(19);
    }

    /// <summary>Removing a chip removes its text from the draft, with one separating space.</summary>
    [Fact]
    public async Task RemovingAChipRemovesItsText()
    {
        await TypeAndAcceptAsync("see @App", 8).ConfigureAwait(true);
        var chip = _model.Chips.Single();

        var edit = _model.RemoveChip(chip, "see @src/App.cs now");

        edit.Draft.ShouldBe("see now");
        edit.Caret.ShouldBe(4);
        _model.Chips.ShouldBeEmpty();
        _model.Budget.ShouldBe(MentionBudget.None);
    }

    /// <summary>Deleting the text of a mention from the draft drops its chip; changing other text keeps it.</summary>
    [Fact]
    public async Task DeletingTheTextDropsTheChip()
    {
        await TypeAndAcceptAsync("see @App", 8).ConfigureAwait(true);

        _model.UpdateDraft("see @src/App.cs more", 20);
        _model.Chips.Count.ShouldBe(1);
        _model.UpdateDraft("see more", 8);
        _model.Chips.ShouldBeEmpty();
    }

    /// <summary>Attaching the same item twice keeps one chip.</summary>
    [Fact]
    public async Task TheSameItemTwiceIsOneChip()
    {
        await TypeAndAcceptAsync("@App", 4).ConfigureAwait(true);
        await TypeAndAcceptAsync("@src/App.cs @App", 16).ConfigureAwait(true);

        _model.Chips.Count.ShouldBe(1);
    }

    /// <summary>A file that may hold secrets needs a second accept, and the second attaches it.</summary>
    [Fact]
    public async Task ASecretFileNeedsASecondAccept()
    {
        _model.UpdateDraft("@.env", 5);
        await _model.Picker.WaitSettledAsync().ConfigureAwait(true);

        (await _model.AcceptSelectedAsync("@.env", Token).ConfigureAwait(true)).ShouldBeNull();
        _model.Picker.IsConfirming.ShouldBeTrue();
        _model.Chips.ShouldBeEmpty();

        var edit = await _model.AcceptSelectedAsync("@.env", Token).ConfigureAwait(true);

        edit.ShouldNotBeNull().Draft.ShouldBe("@.env ");
        _model.Chips.Single().Token.ShouldBe("@.env");
    }

    /// <summary>A disabled row attaches nothing.</summary>
    [Fact]
    public async Task ADisabledRowAttachesNothing()
    {
        _world.WorkingSummary = new MentionGitDiffSummary(false, 0, null, null);
        _model.UpdateDraft("@Commit", 7);
        await _model.Picker.WaitSettledAsync().ConfigureAwait(true);

        (await _model.AcceptSelectedAsync("@Commit", Token).ConfigureAwait(true)).ShouldBeNull();

        _model.Chips.ShouldBeEmpty();
        _model.Picker.Message.ShouldBe(MentionCopy.NoRepository);
    }

    /// <summary>A message carries a limited number of mentions; the next one is refused with a sentence.</summary>
    [Fact]
    public async Task TheMentionLimitIsEnforced()
    {
        for (var index = 0; index < MentionLimits.MaxMentionsPerMessage + 1; index++)
        {
            _world.AddFile("many/f" + index + ".txt");
        }

        var refused = false;
        var draft = string.Empty;
        for (var index = 0; index < MentionLimits.MaxMentionsPerMessage + 1; index++)
        {
            var typed = draft + "@many/f" + index + ".txt";
            var edit = await TypeAndAcceptAsync(typed, typed.Length).ConfigureAwait(true);
            refused = edit is null;
            if (edit is not null)
            {
                draft = edit.Draft;
            }
        }

        _model.Chips.Count.ShouldBe(MentionLimits.MaxMentionsPerMessage);
        refused.ShouldBeTrue();
        _model.Picker.Message.ShouldBe(MentionCopy.TooManyMentions(MentionLimits.MaxMentionsPerMessage));
    }

    /// <summary>The send reads the content of every chip; one that is gone is dropped, named, and explained in the notice.</summary>
    [Fact]
    public async Task CollectingReadsEveryChipAndNamesTheOnesThatFailed()
    {
        await TypeAndAcceptAsync("@App", 4).ConfigureAwait(true);
        await TypeAndAcceptAsync("@src/App.cs @notes", 18).ConfigureAwait(true);
        _model.Chips.Count.ShouldBe(2);

        var all = await _model.CollectAsync(Token).ConfigureAwait(true);
        all.Failures.ShouldBeEmpty();
        all.Attachments.Select(static attachment => attachment.Content.Text).ShouldBe(["class App;", "notes"]);

        _world.Files.Remove("my notes.md");
        var partial = await _model.CollectAsync(Token).ConfigureAwait(true);

        partial.Failures.ShouldBe(["my notes.md"]);
        partial.Attachments.Select(static attachment => attachment.Mention.Display).ShouldBe(["src/App.cs"]);
        _model.Chips.Select(static chip => chip.Mention.Display).ShouldBe(["src/App.cs"]);
        _model.Notice.ShouldBe(MentionCopy.AttachFailed(["my notes.md"]));
    }

    /// <summary>The estimate follows the chips: four bytes a token.</summary>
    [Fact]
    public async Task TheBudgetFollowsTheChips()
    {
        await TypeAndAcceptAsync("@App", 4).ConfigureAwait(true);
        _model.Budget.EstimatedBytes.ShouldBe(10);
        _model.Budget.EstimatedTokens.ShouldBe(3);

        await TypeAndAcceptAsync("@src/App.cs @notes", 18).ConfigureAwait(true);

        _model.Budget.EstimatedBytes.ShouldBe(15);
        _model.Budget.EstimatedTokens.ShouldBe(4);
        _model.Clear();
        _model.HasChips.ShouldBeFalse();
    }

    /// <summary>An item an ignore rule excludes after the search cannot be attached: the picker says why.</summary>
    [Fact]
    public async Task AnIgnoredItemIsRefusedWithTheReason()
    {
        var ignored = false;
        using var model = new MentionComposerModel([new FileMentionSource(_world, new FlagIgnore(() => ignored))]);
        model.UpdateDraft("@App", 4);
        await model.Picker.WaitSettledAsync().ConfigureAwait(true);
        ignored = true;

        (await model.AcceptSelectedAsync("@App", Token).ConfigureAwait(true)).ShouldBeNull();

        model.Picker.Message.ShouldBe(MentionCopy.IgnoredByRule("src/App.cs"));
        model.Chips.ShouldBeEmpty();
    }

    private sealed class FlagIgnore(Func<bool> ignored) : IMentionIgnoreFilter
    {
        public bool IsIgnored(string relativePath, bool isDirectory) => ignored();
    }
}
