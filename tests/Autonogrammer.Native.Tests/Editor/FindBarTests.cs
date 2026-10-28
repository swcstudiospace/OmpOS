using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Autonogrammer.Native.Views.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Editor;

/// <summary>
/// The find bar: the match count, the selected match in the editor, and the moves between matches (ADE-09). The count and
/// the offsets are checked against an independent scan of the fixture text.
/// </summary>
public sealed class FindBarTests
{
    private const string Fixture = "data/fixture.txt";
    private const string LowercaseNeedle = "needle";
    private const int InsensitiveCount = 2000;
    private const int SensitiveCount = 1000;

    /// <summary>On a 10000-line fixture the case-insensitive count is 2000 and the match-case count is 1000.</summary>
    [Fact]
    public async Task CountOnTenThousandLinesEqualsTheExpectedOccurrences()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var text = EditorHarness.TenThousandLineFixture();
            var model = EditorHarness.NewModel();
            model.Open(Fixture, text);
            using var window = EditorHarness.Show(model);

            EditorHarness.OccurrenceOffsets(text, EditorHarness.Needle, StringComparison.OrdinalIgnoreCase).Count.ShouldBe(InsensitiveCount);
            EditorHarness.OccurrenceOffsets(text, EditorHarness.Needle, StringComparison.Ordinal).Count.ShouldBe(SensitiveCount);

            EditorHarness.Type(window, EditorHarness.Needle);
            EditorHarness.Status(window).Text.ShouldBe("1 of 2000");
            model.Find!.MatchCount.ShouldBe(InsensitiveCount);

            EditorHarness.SetMatchCase(window, true);
            EditorHarness.Status(window).Text.ShouldBe("1 of 1000");
            model.Find!.MatchCount.ShouldBe(SensitiveCount);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter and Shift+Enter move the selected match forward and back, wrapping at both ends, and the editor selects each one.</summary>
    [Fact]
    public async Task EnterAndShiftEnterMoveBetweenMatchesAndWrap()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var text = EditorHarness.TenThousandLineFixture();
            var offsets = EditorHarness.OccurrenceOffsets(text, EditorHarness.Needle, StringComparison.OrdinalIgnoreCase);
            var model = EditorHarness.NewModel();
            model.Open(Fixture, text);
            using var window = EditorHarness.Show(model);

            EditorHarness.Type(window, EditorHarness.Needle);
            EditorHarness.FindInput(window).Focus();
            AssertSelectedMatch(window, "1 of 2000", offsets[0]);

            EditorHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            AssertSelectedMatch(window, "2 of 2000", offsets[1]);

            EditorHarness.Press(window, Key.Enter, PhysicalKey.Enter, shift: true);
            AssertSelectedMatch(window, "1 of 2000", offsets[0]);

            EditorHarness.Press(window, Key.Enter, PhysicalKey.Enter, shift: true);
            AssertSelectedMatch(window, "2000 of 2000", offsets[^1]);

            EditorHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            AssertSelectedMatch(window, "1 of 2000", offsets[0]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>F3 and Shift+F3 move the same way from the editor, and the Next and Previous buttons move the same way.</summary>
    [Fact]
    public async Task F3AndTheMatchButtonsMoveBetweenMatches()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var text = EditorHarness.TenThousandLineFixture();
            var offsets = EditorHarness.OccurrenceOffsets(text, EditorHarness.Needle, StringComparison.OrdinalIgnoreCase);
            var model = EditorHarness.NewModel();
            model.Open(Fixture, text);
            using var window = EditorHarness.Show(model);

            EditorHarness.Type(window, EditorHarness.Needle);
            EditorHarness.Code(window).TextArea.Focus();

            EditorHarness.Press(window, Key.F3, PhysicalKey.F3);
            AssertSelectedMatch(window, "2 of 2000", offsets[1]);

            EditorHarness.Press(window, Key.F3, PhysicalKey.F3, shift: true);
            AssertSelectedMatch(window, "1 of 2000", offsets[0]);

            EditorHarness.Press(window, Key.F3, PhysicalKey.F3, shift: true);
            AssertSelectedMatch(window, "2000 of 2000", offsets[^1]);

            EditorHarness.NextButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            EditorHarness.Settle(window);
            AssertSelectedMatch(window, "1 of 2000", offsets[0]);

            EditorHarness.PreviousButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            EditorHarness.Settle(window);
            AssertSelectedMatch(window, "2000 of 2000", offsets[^1]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With match case on, navigation visits only the lowercase occurrences.</summary>
    [Fact]
    public async Task MatchCaseNavigatesOnlyCaseSensitiveOccurrences()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var text = EditorHarness.TenThousandLineFixture();
            var offsets = EditorHarness.OccurrenceOffsets(text, EditorHarness.Needle, StringComparison.Ordinal);
            var model = EditorHarness.NewModel();
            model.Open(Fixture, text);
            using var window = EditorHarness.Show(model);

            EditorHarness.SetMatchCase(window, true);
            EditorHarness.Type(window, EditorHarness.Needle);
            AssertSelectedMatch(window, "1 of 1000", offsets[0]);

            EditorHarness.FindInput(window).Focus();
            EditorHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            AssertSelectedMatch(window, "2 of 1000", offsets[1]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A query with no occurrence shows "No matches", disables both match buttons and selects nothing.</summary>
    [Fact]
    public async Task QueryWithNoOccurrenceShowsNoMatches()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(Fixture, "alpha beta\ngamma\n");
            using var window = EditorHarness.Show(model);

            EditorHarness.Type(window, "delta");

            EditorHarness.Status(window).Text.ShouldBe("No matches");
            EditorHarness.NextButton(window).IsEnabled.ShouldBeFalse();
            EditorHarness.PreviousButton(window).IsEnabled.ShouldBeFalse();
            model.Find!.SelectedMatch.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Measures the find on the 10000-line fixture: the time from setting the query on the model to the match count and the
    /// selected match being up to date. The figures are written to the test output and recorded in ADR-0007. The bound only
    /// catches a find that stops being interactive.
    /// </summary>
    [Fact]
    public async Task FindOnTenThousandLinesIsInteractive()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(Fixture, EditorHarness.TenThousandLineFixture());
            using var window = EditorHarness.Show(model);

            var first = Measure(model, window, LowercaseNeedle, InsensitiveCount);
            var second = Measure(model, window, "row", 10000);
            var third = Measure(model, window, "Needle", InsensitiveCount);

            var report = $"find timing on 10000 lines: '{LowercaseNeedle}' {first.TotalMilliseconds:F3} ms ({InsensitiveCount} matches), " +
                $"'row' {second.TotalMilliseconds:F3} ms (10000 matches), 'Needle' {third.TotalMilliseconds:F3} ms ({InsensitiveCount} matches)";
            TestContext.Current.SendDiagnosticMessage(report);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "editor-find-timing.txt"), report + Environment.NewLine);

            first.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            second.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            third.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    // Times one query on the model and checks that the status shows the expected count once the find has run.
    private static TimeSpan Measure(EditorViewModel model, Window window, string query, int expectedCount)
    {
        var clock = Stopwatch.StartNew();
        model.FindQuery = query;
        clock.Stop();

        EditorHarness.Status(window).Text.ShouldBe($"1 of {expectedCount}");
        return clock.Elapsed;
    }

    // Asserts the status line and the editor's selection after a move. The selection must be the match the find selected.
    private static void AssertSelectedMatch(Window window, string status, int offset)
    {
        EditorHarness.Status(window).Text.ShouldBe(status);
        EditorHarness.Code(window).SelectionStart.ShouldBe(offset);
        EditorHarness.Code(window).SelectionLength.ShouldBe(EditorHarness.Needle.Length);
    }
}
