using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Export;
using Autonogrammer.Native.Views.Usage;
using Autonogrammer.Presentation.Export;
using Autonogrammer.Presentation.Usage;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Usage;

/// <summary>The usage view and the export history the main view lists.</summary>
public sealed class UsageMeterViewTests
{
    /// <summary>CreateExport lists the accumulated history, and an empty source keeps the previous empty shape.</summary>
    [Fact]
    public async Task CreateExportListsTheAccumulatedHistoryAndAnEmptyLedgerStaysEmpty()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var day = new DateOnly(2026, 10, 1);
            var history = UsageMeterModel.ToHistory(
            [
                new UsageMeterModel.Sample("session-a", day, 1000, 100, 1.00m, "USD"),
                new UsageMeterModel.Sample("session-a", day, 1000, 250, 1.50m, "USD"),
            ]);
            var main = new MainView();
            main.UsageHistorySource = new FixedHistory(history);
            var export = Show(main.CreateExport());
            var totals = (IReadOnlyList<UsageTotal>)export.FindControl<ListBox>("UsageList")!.ItemsSource!;
            totals.ShouldHaveSingleItem().InputTokens.ShouldBe(250);
            totals[0].Period.ShouldBe("2026-10-01");
            export.FindControl<TextBlock>("UsageEmptyText")!.IsVisible.ShouldBeFalse();

            main.UsageHistorySource = new FixedHistory(new UsageHistory([]));
            var empty = Show(main.CreateExport());
            empty.FindControl<TextBlock>("UsageEmptyText")!.IsVisible.ShouldBeTrue();
            empty.FindControl<TextBlock>("UsageEmptyText")!.Text.ShouldBe("No usage recorded.");
            ((IReadOnlyList<UsageTotal>)empty.FindControl<ListBox>("UsageList")!.ItemsSource!).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The view names per-session and per-day sums, and shows the unavailable state.</summary>
    [Fact]
    public async Task TheViewRendersSessionAndDaySumsAndTheUnavailableState()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var day = new DateOnly(2026, 10, 2);
            var model = UsageMeterModel.Create(
            [
                new UsageMeterModel.Sample("alpha", day, 1000, 40, 0.5m, "USD"),
                new UsageMeterModel.Sample("alpha", day, 1000, 90, 0.8m, "USD"),
            ]);
            var view = Show(new UsageMeterView(model));
            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToArray();
            texts.ShouldContain(text => text.Contains("alpha", StringComparison.Ordinal) && text.Contains("last 90", StringComparison.Ordinal) && text.Contains("peak 90", StringComparison.Ordinal) && text.Contains("0.8", StringComparison.Ordinal));
            AutomationProperties.GetName(view.GetVisualDescendants().OfType<TextBlock>().First(block => (block.Text ?? string.Empty).Contains("alpha", StringComparison.Ordinal))).ShouldBe("Session alpha");
            AutomationProperties.GetName(view.GetVisualDescendants().OfType<TextBlock>().First(block => (block.Text ?? string.Empty).StartsWith("2026-10-02", StringComparison.Ordinal))).ShouldBe("Day 2026-10-02");

            var missing = Show(new UsageMeterView(UsageMeterModel.Create([new UsageMeterModel.Sample("beta", day, 10, 1, null, null)])));
            missing.Unavailable.IsVisible.ShouldBeTrue();
            missing.Unavailable.Text.ShouldBe(UsageMeterModel.UsageUnavailable);
            AutomationProperties.GetName(missing.Unavailable).ShouldBe("Usage unavailable");
            missing.ToString().ShouldNotContain("0.8");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Log templates and ToString in the new usage files do not carry occupancy or amount field names.</summary>
    [Fact]
    public void LogsAndToStringDoNotCarryUsageFigures()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "src/Autonogrammer.Harness.AgentLoop/Usage/FileUsageLedger.cs",
            "src/Autonogrammer.Native.Views/Views/Usage/UsageMeterView.axaml.cs",
            "src/Autonogrammer.Presentation/Usage/UsageMeterModel.cs",
        };
        foreach (var relative in files)
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            foreach (var literal in StringLiterals(text))
            {
                if (literal.Contains("Usage ledger", StringComparison.Ordinal) || literal.Contains("LoggerMessage", StringComparison.Ordinal))
                {
                    literal.ShouldNotContain("size", Case.Insensitive);
                    literal.ShouldNotContain("cost", Case.Insensitive);
                }
            }

            text.ShouldNotContain("override string ToString() => $\"");
        }
    }

    private static T Show<T>(T control)
        where T : Control
    {
        var window = new Window { Content = control, Width = 800, Height = 600 };
        window.Show();
        return control;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Autonogrammer.Harness.AgentLoop", "Usage", "FileUsageLedger.cs")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not find the usage ledger source.");
    }

    private static IEnumerable<string> StringLiterals(string text)
    {
        var start = 0;
        while ((start = text.IndexOf('"', start)) >= 0)
        {
            var end = text.IndexOf('"', start + 1);
            if (end < 0)
            {
                yield break;
            }

            yield return text[start..(end + 1)];
            start = end + 1;
        }
    }

    private sealed class FixedHistory(UsageHistory history) : IUsageHistorySource
    {
        public UsageHistory Read() => history;
    }
}
