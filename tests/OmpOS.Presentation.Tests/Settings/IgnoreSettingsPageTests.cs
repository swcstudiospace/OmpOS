using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Ignore;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The Ignore page: the draft list and switches, what is refused, dirty/save/reset/discard, and the read-only built-in list and files.</summary>
public sealed class IgnoreSettingsPageTests
{
    private static async Task<(IgnoreSettingsPage Page, ScriptedIgnoreSource Source)> LoadedAsync(IgnoreSettingsValues? seed = null)
    {
        var source = new ScriptedIgnoreSource();
        if (seed is not null)
        {
            source.Seed(seed);
        }

        var page = new IgnoreSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source);
    }

    /// <summary>The defaults are no global patterns, the built-in list on and folders below the root off.</summary>
    [Fact]
    public async Task StartsWithTheDefaults()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Section.ShouldBe(SettingsSection.Ignore);
        page.Patterns.ShouldBeEmpty();
        page.IsEmpty.ShouldBeTrue();
        page.UseDefaultList.ShouldBeTrue();
        page.HierarchicalIgnoreFiles.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Adding trims the pattern, makes the page dirty and writes nothing until Save.</summary>
    [Fact]
    public async Task AddingIsADraftUntilSave()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(page);

        page.AddPattern("  build/  ").ShouldBeTrue();

        page.Patterns.ShouldBe(["build/"]);
        page.IsDirty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(IgnoreSettingsPage.Patterns));
        source.SaveCalls.ShouldBe(0);

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        source.Values.GlobalPatterns.ShouldBe(["build/"]);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Blank, over-long, multi-line and duplicate patterns are refused with a reason, the draft stays as it was, and a good add clears the reason.</summary>
    [Fact]
    public async Task RefusedPatternsAreNotAddedAndTheReasonShows()
    {
        var (page, _) = await LoadedAsync().ConfigureAwait(true);
        page.AddPattern("*.log").ShouldBeTrue();

        page.AddPattern("   ").ShouldBeFalse();
        page.EntryError.ShouldBe(IgnoreCopy.BlankPattern, Case.Sensitive);
        page.AddPattern(null).ShouldBeFalse();
        page.AddPattern(new string('a', IgnoreSettingsLimits.MaxPatternLength + 1)).ShouldBeFalse();
        page.EntryError.ShouldBe(IgnoreCopy.PatternTooLong(IgnoreSettingsLimits.MaxPatternLength), Case.Sensitive);
        page.AddPattern("a\nb").ShouldBeFalse();
        page.EntryError.ShouldBe(IgnoreCopy.PatternHasLineBreak, Case.Sensitive);
        page.AddPattern(" *.log ").ShouldBeFalse();
        page.EntryError.ShouldBe(IgnoreCopy.DuplicatePattern, Case.Sensitive);
        page.HasEntryError.ShouldBeTrue();
        page.Patterns.ShouldBe(["*.log"]);

        page.AddPattern(new string('a', IgnoreSettingsLimits.MaxPatternLength)).ShouldBeTrue();
        page.HasEntryError.ShouldBeFalse();
    }

    /// <summary>The list holds at most 500 patterns: the 501st is refused.</summary>
    [Fact]
    public async Task TheListIsCappedAtFiveHundred()
    {
        var seed = new IgnoreSettingsValues([.. Enumerable.Range(0, IgnoreSettingsLimits.MaxPatterns).Select(static i => "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))], true, false);
        var (page, _) = await LoadedAsync(seed).ConfigureAwait(true);

        page.AddPattern("one-more").ShouldBeFalse();

        page.EntryError.ShouldBe(IgnoreCopy.TooManyPatterns(IgnoreSettingsLimits.MaxPatterns), Case.Sensitive);
        page.Patterns.Count.ShouldBe(IgnoreSettingsLimits.MaxPatterns);
    }

    /// <summary>Patterns that are each allowed but together hold too many characters are refused, so the settings file stays small.</summary>
    [Fact]
    public async Task TheTotalLengthIsCapped()
    {
        var longPattern = new string('x', IgnoreSettingsLimits.MaxPatternLength);
        var seed = new IgnoreSettingsValues([.. Enumerable.Range(0, 32).Select(i => longPattern[..^2] + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))], true, false);
        var (page, _) = await LoadedAsync(seed).ConfigureAwait(true);

        page.AddPattern("y").ShouldBeFalse();

        page.EntryError.ShouldBe(IgnoreCopy.ListTooLong, Case.Sensitive);
    }

    /// <summary>Removing deletes exactly the pattern named, in order, and an unknown pattern changes nothing.</summary>
    [Fact]
    public async Task RemovingTakesOutOnlyThatPattern()
    {
        var (page, _) = await LoadedAsync(new IgnoreSettingsValues(["a", "b", "c"], true, false)).ConfigureAwait(true);

        page.RemovePattern("zzz").ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
        page.RemovePattern("b").ShouldBeTrue();

        page.Patterns.ShouldBe(["a", "c"]);
        page.IsDirty.ShouldBeTrue();
    }

    /// <summary>The dirty check compares lists by sequence: re-adding what was removed is not a change, a different order is.</summary>
    [Fact]
    public async Task DirtyComparesTheListsBySequence()
    {
        var (page, _) = await LoadedAsync(new IgnoreSettingsValues(["a", "b"], true, false)).ConfigureAwait(true);

        page.RemovePattern("b").ShouldBeTrue();
        page.AddPattern("b").ShouldBeTrue();

        page.IsDirty.ShouldBeFalse("the draft holds the same patterns in the same order");

        page.RemovePattern("a").ShouldBeTrue();
        page.AddPattern("a").ShouldBeTrue();

        page.Patterns.ShouldBe(["b", "a"]);
        page.IsDirty.ShouldBeTrue("the same patterns in another order are a change");
    }

    /// <summary>The two switches are drafts too, and switching back is not a change.</summary>
    [Fact]
    public async Task TheSwitchesAreDrafts()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await LoadedAsync().ConfigureAwait(true);

        page.SetUseDefaultList(false);
        page.SetHierarchicalIgnoreFiles(true);

        page.IsDirty.ShouldBeTrue();
        source.SaveCalls.ShouldBe(0);
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        source.Values.UseDefaultList.ShouldBeFalse();
        source.Values.HierarchicalIgnoreFiles.ShouldBeTrue();

        page.SetUseDefaultList(true);
        page.SetUseDefaultList(false);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Reset returns the draft to the defaults and clears the reason; Discard returns it to the saved values and clears the reason.</summary>
    [Fact]
    public async Task ResetAndDiscardClearTheReasonAndTheDraft()
    {
        var (page, _) = await LoadedAsync(new IgnoreSettingsValues(["saved"], false, true)).ConfigureAwait(true);
        page.AddPattern("extra").ShouldBeTrue();
        page.AddPattern(string.Empty).ShouldBeFalse();

        page.Reset();

        page.Draft.ShouldBe(IgnoreSettingsValues.Defaults);
        page.HasEntryError.ShouldBeFalse();
        page.IsDirty.ShouldBeTrue("the defaults differ from what is saved");

        page.AddPattern(string.Empty).ShouldBeFalse();
        page.Discard();

        page.Patterns.ShouldBe(["saved"]);
        page.UseDefaultList.ShouldBeFalse();
        page.HasEntryError.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A rejected write keeps the draft and the old saved values and shows the reason.</summary>
    [Fact]
    public async Task ARejectedWriteKeepsTheDraft()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source) = await LoadedAsync().ConfigureAwait(true);
        source.SaveFailure = "settings file is locked";
        page.AddPattern("tmp/").ShouldBeTrue();

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();

        page.SaveError.ShouldBe("settings file is locked", Case.Sensitive);
        page.IsDirty.ShouldBeTrue();
        page.Saved.GlobalPatterns.ShouldBeEmpty();
        page.Patterns.ShouldBe(["tmp/"]);
    }

    /// <summary>A failed load is the error state and nothing can be edited.</summary>
    [Fact]
    public async Task AFailedLoadAllowsNoEdit()
    {
        var source = new ScriptedIgnoreSource { LoadFailure = "settings file is damaged" };
        var page = new IgnoreSettingsPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.AddPattern("x").ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>The built-in list and the found files come from the source and are read-only: the page offers no way to change them.</summary>
    [Fact]
    public async Task ShowsTheBuiltInListAndTheFilesFound()
    {
        var source = new ScriptedIgnoreSource { Files = [new IgnoreFileRow(".cursorignore", 3, ["Line 2: too long"]), new IgnoreFileRow(".gitignore", 1, [])] };
        var page = new IgnoreSettingsPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.DefaultPatterns.ShouldBe([".git/", "node_modules/", "*.png"]);
        page.Files.Select(static file => file.Summary).ShouldBe([".cursorignore: 3 rules, 1 skipped.", ".gitignore: 1 rule."]);
        page.Files[0].HasProblems.ShouldBeTrue();
        page.Files[1].HasProblems.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }
}
