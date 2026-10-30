using System.Text.Json;
using Autonogrammer.Presentation.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>The bounded run log: newest first, filterable, redacted and copyable as JSON.</summary>
public sealed class HookRunLogModelTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Rows are listed newest first with the time in the log's zone and the words for the outcome and the duration.</summary>
    [Fact]
    public void RowsAreNewestFirstWithTimesInTheLogsZone()
    {
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Append(HookItems.Run("first", at: Noon));
        log.Append(HookItems.Run("second", HookRunOutcome.Denied, 7, at: Noon.AddMinutes(5)));

        log.Rows.Select(static r => r.HookName).ToArray().ShouldBe(["second", "first"]);
        log.Rows[0].TimeText.ShouldBe("12:05:00");
        log.Rows[0].OutcomeText.ShouldBe("Denied");
        log.Rows[0].DurationText.ShouldBe("7 ms");
        log.Rows[0].IsProblem.ShouldBeTrue();
        log.Rows[1].IsProblem.ShouldBeFalse();
        log.TotalCount.ShouldBe(2);
    }

    /// <summary>Only the newest 500 runs are kept.</summary>
    [Fact]
    public void OnlyTheNewestFiveHundredRunsAreKept()
    {
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Replace(Enumerable.Range(0, 520).Select(static i => HookItems.Run("hook-" + i)));
        log.Append(HookItems.Run("latest"));

        log.TotalCount.ShouldBe(HookRunLogModel.Capacity);
        log.Rows[0].HookName.ShouldBe("latest");
        log.Rows[^1].HookName.ShouldBe("hook-21");
    }

    /// <summary>The filter keeps the runs of one outcome, says when it hides everything, and maps to its index in the list of choices.</summary>
    [Fact]
    public void TheFilterShowsOneOutcome()
    {
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.EmptyText.ShouldBe(HooksCopy.RunLogEmpty);
        log.Replace([HookItems.Run("a"), HookItems.Run("b", HookRunOutcome.TimedOut), HookItems.Run("c", HookRunOutcome.TimedOut)]);

        log.SelectedFilterIndex = log.FilterChoices.ToList().IndexOf(HooksCopy.OutcomeText(HookRunOutcome.TimedOut));

        log.Filter.ShouldBe(HookRunOutcome.TimedOut);
        log.Rows.Select(static r => r.HookName).ToArray().ShouldBe(["c", "b"]);
        log.SelectedFilterIndex.ShouldBe(4);
        log.SetFilter(HookRunOutcome.Denied);
        log.Rows.ShouldBeEmpty();
        log.IsFilteredEmpty.ShouldBeTrue();
        log.EmptyText.ShouldBe(HooksCopy.RunLogNoMatch);
        log.SelectedFilterIndex = 0;
        log.Filter.ShouldBeNull();
        log.Rows.Count.ShouldBe(3);
        log.FilterChoices[0].ShouldBe(HooksCopy.RunLogAll);
        log.FilterChoices.Count.ShouldBe(10);
        log.SelectedFilterIndex = 99;
        log.Filter.ShouldBeNull();
    }

    /// <summary>Output that looks like a credential is redacted in the rows and in the JSON, and a cut output says so.</summary>
    [Fact]
    public void OutputIsRedactedInTheRowsAndInTheJson()
    {
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Append(HookItems.Run("guard", stdout: "deploying with api_key=sk-live-1234567890abcdef now") with { OutputTruncated = true, UserMessage = "Authorization: Bearer abcdefghijklmnop", StandardErrorExcerpt = "password=hunter2hunter2" });

        var row = log.Rows.Single();
        row.Output.ShouldNotContain("sk-live-1234567890abcdef");
        row.Output.ShouldContain("[redacted]");
        row.Output.ShouldEndWith(HooksCopy.OutputCut);
        row.UserMessage.ShouldNotContain("abcdefghijklmnop");
        row.ErrorOutput.ShouldNotContain("hunter2hunter2");
        row.HasDetails.ShouldBeTrue();
        var json = log.ToJson();
        json.ShouldNotContain("sk-live-1234567890abcdef");
        json.ShouldNotContain("hunter2hunter2");
    }

    /// <summary>The copy is a JSON array of the visible rows, newest first, with the contract's names, and it honours the filter.</summary>
    [Fact]
    public void TheCopyIsAJsonArrayOfTheVisibleRows()
    {
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Replace([HookItems.Run("a", durationMs: 5), HookItems.Run("b", HookRunOutcome.TimedOut, 30000)]);

        using var all = JsonDocument.Parse(log.ToJson());
        all.RootElement.GetArrayLength().ShouldBe(2);
        all.RootElement[0].GetProperty("hookId").GetString().ShouldBe("b");
        all.RootElement[0].GetProperty("status").GetString().ShouldBe("timedOut");
        all.RootElement[0].GetProperty("scope").GetString().ShouldBe("project");
        all.RootElement[0].GetProperty("durationMs").GetInt64().ShouldBe(30000);

        log.SetFilter(HookRunOutcome.Succeeded);
        using var one = JsonDocument.Parse(log.ToJson());
        one.RootElement.GetArrayLength().ShouldBe(1);
        one.RootElement[0].GetProperty("hookId").GetString().ShouldBe("a");
    }
}
