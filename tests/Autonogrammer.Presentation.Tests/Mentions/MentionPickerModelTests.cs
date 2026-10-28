using Autonogrammer.Presentation.Mentions;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mentions;

/// <summary>The mention picker: opening on a token, grouping, selection, stale answers, timeouts, failures, the empty and unavailable states, dismissing and confirming.</summary>
public sealed class MentionPickerModelTests
{
    private static MentionToken Token(string query, int start = 0, MentionTrigger trigger = MentionTrigger.At) =>
        new(trigger, query, start, query.Length + 1, false, true);

    private static MentionCandidate Row(MentionKind kind, string id, string? reason = null, bool confirm = false) =>
        new(kind, id, id, null, 0, confirm, reason);

    private static MentionPickerModel Picker(params IMentionSource[] sources) => new(sources);

    /// <summary>A token opens the picker, the rows come in group order with one group start each, the first row is selected and the combobox state names it.</summary>
    [Fact]
    public async Task ATokenOpensThePickerWithGroupedRows()
    {
        using var picker = Picker(
            ScriptedMentionSource.Returning(MentionKind.Terminal, Row(MentionKind.Terminal, "t1")),
            ScriptedMentionSource.Returning(MentionKind.File, Row(MentionKind.File, "a.cs"), Row(MentionKind.File, "b.cs")));

        picker.Update(Token(string.Empty));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.IsOpen.ShouldBeTrue();
        picker.Rows.Select(static row => row.Candidate.Id).ShouldBe(["a.cs", "b.cs", "t1"]);
        picker.Rows.Select(static row => row.IsGroupStart).ShouldBe([true, false, true]);
        picker.Groups.Select(static group => group.Title).ShouldBe(["Files", "Terminals"]);
        picker.SelectedIndex.ShouldBe(0);
        picker.ActiveDescendantId.ShouldBe("mention-option-0");
        picker.Announcement.ShouldBe("3 items");
        picker.Message.ShouldBeNull();
        picker.IsLoading.ShouldBeFalse();
    }

    /// <summary>The selection wraps past either end and the active descendant follows it.</summary>
    [Fact]
    public async Task TheSelectionWraps()
    {
        using var picker = Picker(ScriptedMentionSource.Returning(MentionKind.File, Row(MentionKind.File, "a"), Row(MentionKind.File, "b"), Row(MentionKind.File, "c")));
        picker.Update(Token(string.Empty));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.MoveSelection(-1);
        picker.SelectedIndex.ShouldBe(2);
        picker.ActiveDescendantId.ShouldBe("mention-option-2");
        picker.MoveSelection(1);
        picker.SelectedIndex.ShouldBe(0);
        picker.Select(1).ShouldBeTrue();
        picker.SelectedRow!.Candidate.Id.ShouldBe("b");
        picker.Select(9).ShouldBeFalse();
    }

    /// <summary>Only the sources of the token's trigger are asked, and the per-source limit follows the query: small when empty, larger when typed, largest inside a folder.</summary>
    [Fact]
    public async Task OnlyTheSourcesOfTheTriggerAreAskedWithALimitThatFollowsTheQuery()
    {
        var at = ScriptedMentionSource.Returning(MentionKind.File);
        var hash = new ScriptedMentionSource(MentionKind.PullRequest, (_, _, _) => Task.FromResult(MentionSearchResult.Empty), MentionTrigger.Hash);
        using var picker = Picker(at, hash);

        picker.Update(Token(string.Empty));
        await picker.WaitSettledAsync().ConfigureAwait(true);
        picker.Update(Token("ab"));
        await picker.WaitSettledAsync().ConfigureAwait(true);
        picker.Update(Token("src/"));
        await picker.WaitSettledAsync().ConfigureAwait(true);
        picker.Update(Token("12", trigger: MentionTrigger.Hash));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        at.Limits.ShouldBe([MentionLimits.RowsPerSourceEmpty, MentionLimits.RowsPerSourceTyped, MentionLimits.RowsPerSourceTyped * 3]);
        hash.Queries.ShouldBe(["12"]);
    }

    /// <summary>A later query wins: the answer of the older one arrives afterwards and is dropped.</summary>
    [Fact]
    public async Task AStaleAnswerIsDropped()
    {
        var gates = new Dictionary<string, TaskCompletionSource<MentionSearchResult>>(StringComparer.Ordinal)
        {
            ["a"] = new(TaskCreationOptions.RunContinuationsAsynchronously),
            ["ab"] = new(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        using var picker = Picker(new ScriptedMentionSource(MentionKind.File, (query, _, _) => gates[query].Task.WaitAsync(CancellationToken.None)));

        picker.Update(Token("a"));
        var first = picker.WaitSettledAsync();
        picker.Update(Token("ab"));
        gates["ab"].SetResult(new MentionSearchResult([Row(MentionKind.File, "ab.cs")]));
        await picker.WaitSettledAsync().ConfigureAwait(true);
        gates["a"].SetResult(new MentionSearchResult([Row(MentionKind.File, "a.cs"), Row(MentionKind.File, "another.cs")]));
        await first.ConfigureAwait(true);

        picker.Rows.Select(static row => row.Candidate.Id).ShouldBe(["ab.cs"]);
        picker.Query.ShouldBe("ab");
    }

    /// <summary>While the first search runs the message says so; a source that does not answer in time is dropped and the others still show.</summary>
    [Fact]
    public async Task ASourceThatTimesOutDoesNotHideTheOthers()
    {
        var clock = new ManualTimeProvider();
        var hung = new ScriptedMentionSource(MentionKind.File, async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            return MentionSearchResult.Empty;
        });
        using var picker = new MentionPickerModel([hung, ScriptedMentionSource.Returning(MentionKind.Terminal, Row(MentionKind.Terminal, "t1"))], clock, TimeSpan.FromSeconds(3));

        picker.Update(Token(string.Empty));
        picker.IsLoading.ShouldBeTrue();
        picker.MessageKind.ShouldBe(MentionMessageKind.Searching);
        clock.Advance(TimeSpan.FromSeconds(3));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.Rows.Select(static row => row.Candidate.Id).ShouldBe(["t1"]);
        picker.IsLoading.ShouldBeFalse();
        picker.MessageKind.ShouldBe(MentionMessageKind.None);
    }

    /// <summary>When every source fails the message says so with the reason; when none matches it says that; a source with no host says why.</summary>
    [Fact]
    public async Task ErrorEmptyAndUnavailableStatesCarryTheirText()
    {
        using var failing = Picker(new ScriptedMentionSource(MentionKind.File, (_, _, _) => Task.FromException<MentionSearchResult>(new IOException("disk gone"))));
        failing.Update(Token("x"));
        await failing.WaitSettledAsync().ConfigureAwait(true);
        failing.Rows.ShouldBeEmpty();
        failing.MessageKind.ShouldBe(MentionMessageKind.Error);
        failing.Message.ShouldBe("Search failed: disk gone");

        using var none = Picker(ScriptedMentionSource.Returning(MentionKind.File));
        none.Update(Token("x"));
        await none.WaitSettledAsync().ConfigureAwait(true);
        none.MessageKind.ShouldBe(MentionMessageKind.Empty);
        none.Message.ShouldBe(MentionCopy.NothingMatches);
        none.Announcement.ShouldBe(MentionCopy.NothingMatches);

        using var pulls = Picker(new PullRequestMentionSource(new UnavailablePullRequestSource()));
        pulls.Update(Token("1", trigger: MentionTrigger.Hash));
        await pulls.WaitSettledAsync().ConfigureAwait(true);
        pulls.MessageKind.ShouldBe(MentionMessageKind.Unavailable);
        pulls.Message.ShouldBe(MentionCopy.PullRequestsNoHost);
    }

    /// <summary>A source that throws something unforeseen does not wedge the picker: the failure is shown as the message and the search settles.</summary>
    [Fact]
    public async Task AnUnforeseenFailureIsShownAndTheSearchSettles()
    {
        using var picker = Picker(new ScriptedMentionSource(MentionKind.File, (_, _, _) => Task.FromException<MentionSearchResult>(new InvalidOperationException("boom"))));

        picker.Update(Token("x"));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.IsLoading.ShouldBeFalse();
        picker.MessageKind.ShouldBe(MentionMessageKind.Error);
        picker.Message.ShouldBe("Search failed: boom");
    }

    /// <summary>Dismissing closes the picker for that token only; another token, or no token, reopens it.</summary>
    [Fact]
    public async Task DismissClosesUntilTheTokenChanges()
    {
        using var picker = Picker(ScriptedMentionSource.Returning(MentionKind.File, Row(MentionKind.File, "a.cs")));
        picker.Update(Token("a", start: 4));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.Dismiss();
        picker.IsOpen.ShouldBeFalse();
        picker.Rows.ShouldBeEmpty();

        picker.Update(Token("a", start: 4));
        picker.IsOpen.ShouldBeFalse();

        picker.Update(Token("ab", start: 4));
        picker.IsOpen.ShouldBeTrue();
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.Update(null);
        picker.IsOpen.ShouldBeFalse();
        picker.Update(Token("ab", start: 4));
        picker.IsOpen.ShouldBeTrue();
    }

    /// <summary>A file that may hold secrets is taken only on the second accept; Escape cancels the question before it closes the list; moving away cancels it too.</summary>
    [Fact]
    public async Task ASecretBearingRowNeedsConfirmation()
    {
        using var picker = Picker(ScriptedMentionSource.Returning(MentionKind.File, Row(MentionKind.File, ".env", confirm: true)));
        picker.Update(Token(".env"));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.Accept().ShouldBeNull();
        picker.IsConfirming.ShouldBeTrue();
        picker.MessageKind.ShouldBe(MentionMessageKind.Confirm);
        picker.Message.ShouldBe(MentionCopy.ConfirmSecret(".env"));

        picker.Dismiss();
        picker.IsConfirming.ShouldBeFalse();
        picker.IsOpen.ShouldBeTrue();
        picker.Message.ShouldBeNull();

        picker.Accept().ShouldBeNull();
        picker.Accept().ShouldNotBeNull().Id.ShouldBe(".env");
        picker.IsConfirming.ShouldBeFalse();
    }

    /// <summary>A disabled row is not taken: the message says why, and moving off it clears the message.</summary>
    [Fact]
    public async Task ADisabledRowIsNotTakenAndSaysWhy()
    {
        using var picker = Picker(ScriptedMentionSource.Returning(
            MentionKind.File,
            Row(MentionKind.File, "ok.cs"),
            Row(MentionKind.File, "off.cs", reason: "Not here.")));
        picker.Update(Token(string.Empty));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.MoveSelection(1);
        picker.Message.ShouldBe("Not here.");
        picker.Accept().ShouldBeNull();
        picker.MessageKind.ShouldBe(MentionMessageKind.Unavailable);

        picker.MoveSelection(1);
        picker.Message.ShouldBeNull();
        picker.Accept().ShouldNotBeNull().Id.ShouldBe("ok.cs");
    }

    /// <summary>The list is capped at the row limit however many rows the sources return.</summary>
    [Fact]
    public async Task TheRowCountIsCapped()
    {
        var many = Enumerable.Range(0, 100).Select(index => Row(MentionKind.File, "f" + index)).ToArray();
        using var picker = Picker(ScriptedMentionSource.Returning(MentionKind.File, many));

        picker.Update(Token("f"));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.Rows.Count.ShouldBe(MentionLimits.MaxRows);
        picker.Rows[^1].Index.ShouldBe(MentionLimits.MaxRows - 1);
    }

    /// <summary>A pull request row reads with its hash, and every row has an accessible name that says its kind.</summary>
    [Fact]
    public async Task RowsCarryTheirLabelAndAccessibleName()
    {
        using var picker = Picker(new ScriptedMentionSource(
            MentionKind.PullRequest,
            (_, _, _) => Task.FromResult(new MentionSearchResult([new MentionCandidate(MentionKind.PullRequest, "123", "123", "Fix crash (open)")])),
            MentionTrigger.Hash));

        picker.Update(Token(string.Empty, trigger: MentionTrigger.Hash));
        await picker.WaitSettledAsync().ConfigureAwait(true);

        picker.Rows.Single().Label.ShouldBe("#123");
        picker.Rows.Single().AutomationName.ShouldBe("pull request 123, Fix crash (open)");
        picker.Groups.Single().Title.ShouldBe("Pull requests");
    }
}
