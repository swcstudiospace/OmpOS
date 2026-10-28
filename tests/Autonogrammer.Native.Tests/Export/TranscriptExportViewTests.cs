using System.Text;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Input;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Native.Views.Export;
using Autonogrammer.Presentation.Export;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Export;

/// <summary>
/// The export view writes the transcript Markdown byte for byte to the typed path, reports failures without writing, lists
/// usage totals per period, and keeps every control named and reachable by keyboard.
/// </summary>
public sealed class TranscriptExportViewTests : IDisposable
{
    private static readonly string[] GoldenLines =
    [
        "# Demo chat",
        "",
        "## User",
        "",
        "Hello",
        "",
        "## Assistant",
        "",
        "Hi **there**",
        "",
        "## Code: csharp",
        "",
        "```csharp",
        "Console.WriteLine(1);",
        "```",
        "",
        "## Tool call: read",
        "",
        "Status: done",
        "",
        "Call ID: call-1",
        "",
        "Arguments:",
        "",
        "```json",
        "{\"path\":\"a.txt\"}",
        "```",
        "",
        "## Assistant (in progress)",
        "",
        "partial answer",
    ];

    private static readonly string GoldenMarkdown = string.Join('\n', GoldenLines) + "\n";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "autonogrammer-native-export-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates the directory the export tests write into.</summary>
    public TranscriptExportViewTests() => Directory.CreateDirectory(_directory);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Pressing Export writes exactly the golden Markdown, with no byte-order mark, and reports the path it wrote.</summary>
    [Fact]
    public async Task ExportWritesTheExactGoldenMarkdown()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var path = Path.Combine(_directory, "transcript.md");
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", EmptyUsage());
            using var window = ExportHarness.Show(view);

            ExportHarness.PathInput(view).Text = path;
            ExportHarness.Click(ExportHarness.ExportButton(view));
            await ExportHarness.WaitForAsync(window, () => view.LastExportedPath is not null, "the export to finish").ConfigureAwait(true);

            view.LastExportedPath.ShouldBe(path);
            var bytes = await File.ReadAllBytesAsync(path, Token).ConfigureAwait(true);
            Encoding.UTF8.GetString(bytes).ShouldBe(GoldenMarkdown);
            bytes.Length.ShouldBe(Encoding.UTF8.GetByteCount(GoldenMarkdown));
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The Export button is reached by Tab and pressed with Enter, so a keyboard user can export.</summary>
    [Fact]
    public async Task ExportIsReachableAndPressableFromTheKeyboard()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var path = Path.Combine(_directory, "keyboard.md");
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", EmptyUsage());
            using var window = ExportHarness.Show(view);

            ExportHarness.PathInput(view).Text = path;
            ExportHarness.PathInput(view).Focus();
            ExportHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ExportHarness.ExportButton(view).IsFocused.ShouldBeTrue("Tab from the path reaches Export");

            ExportHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ExportHarness.WaitForAsync(window, () => view.LastExportedPath is not null, "the keyboard export to finish").ConfigureAwait(true);

            var bytes = await File.ReadAllBytesAsync(path, Token).ConfigureAwait(true);
            Encoding.UTF8.GetString(bytes).ShouldBe(GoldenMarkdown);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A path in a folder that does not exist shows the failure, writes nothing and reports no exported path.</summary>
    [Fact]
    public async Task ExportToAMissingFolderShowsTheFailureAndWritesNothing()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var path = Path.Combine(_directory, "missing", "transcript.md");
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", EmptyUsage());
            using var window = ExportHarness.Show(view);

            ExportHarness.PathInput(view).Text = path;
            ExportHarness.Click(ExportHarness.ExportButton(view));
            await ExportHarness.WaitForAsync(window, () => ExportHarness.Status(view).Text is { Length: > 0 }, "the failure report").ConfigureAwait(true);

            view.LastExportedPath.ShouldBeNull();
            File.Exists(path).ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A blank path writes nothing and asks for a path.</summary>
    [Fact]
    public async Task ExportWithABlankPathWritesNothing()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", EmptyUsage());
            using var window = ExportHarness.Show(view);

            ExportHarness.PathInput(view).Text = "   ";
            ExportHarness.Click(ExportHarness.ExportButton(view));
            await ExportHarness.WaitForAsync(window, () => ExportHarness.Status(view).Text is { Length: > 0 }, "the request for a path").ConfigureAwait(true);

            view.LastExportedPath.ShouldBeNull();
            Directory.GetFileSystemEntries(_directory).ShouldBeEmpty();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The usage list shows per-day totals by default, and per-month totals when the month period is chosen.</summary>
    [Fact]
    public async Task UsageTotalsAreShownPerDayAndPerMonth()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", SampleUsage());
            using var window = ExportHarness.Show(view);

            ExportHarness.UsageList(view).ItemCount.ShouldBe(2);
            await ExportHarness.WaitForAsync(window, () => Shows(view, "200"), "the day totals").ConfigureAwait(true);
            Shows(view, "2026-10-21").ShouldBeTrue();
            Shows(view, "240").ShouldBeTrue();
            Shows(view, "2026-10-22").ShouldBeTrue();

            ExportHarness.PeriodSelector(view).SelectedItem = UsagePeriod.Month;
            ExportHarness.Settle(window);

            ExportHarness.UsageList(view).ItemCount.ShouldBe(1);
            await ExportHarness.WaitForAsync(window, () => Shows(view, "440"), "the month totals").ConfigureAwait(true);
            Shows(view, "2026-10").ShouldBeTrue();
            Shows(view, "3").ShouldBeTrue();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With no usage recorded, the list is empty and says so.</summary>
    [Fact]
    public async Task AnEmptyUsageHistoryShowsNoRows()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(() =>
        {
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", EmptyUsage());
            using var window = ExportHarness.Show(view);

            ExportHarness.UsageList(view).ItemCount.ShouldBe(0);
            ExportHarness.UsageEmptyText(view).IsVisible.ShouldBeTrue();
            ExportHarness.Settle(window);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every export control is named, and Tab moves through the path, the Export button, the period and the list.</summary>
    [Fact]
    public async Task EveryExportControlIsNamedAndReachableByTab()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.RunAsync(async () =>
        {
            var view = new TranscriptExportView(GoldenTranscript(), "Demo chat", SampleUsage());
            using var window = ExportHarness.Show(view);

            AutomationProperties.GetName(ExportHarness.PathInput(view)).ShouldNotBeNullOrWhiteSpace();
            AutomationProperties.GetName(ExportHarness.ExportButton(view)).ShouldNotBeNullOrWhiteSpace();
            AutomationProperties.GetName(ExportHarness.Status(view)).ShouldNotBeNullOrWhiteSpace();
            AutomationProperties.GetName(ExportHarness.PeriodSelector(view)).ShouldNotBeNullOrWhiteSpace();
            AutomationProperties.GetName(ExportHarness.UsageList(view)).ShouldNotBeNullOrWhiteSpace();

            ExportHarness.PathInput(view).Focus();
            ExportHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ExportHarness.ExportButton(view).IsFocused.ShouldBeTrue();

            ExportHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ExportHarness.PeriodSelector(view).IsFocused.ShouldBeTrue();

            ExportHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            ExportHarness.HasKeyboardFocusWithin(window, ExportHarness.UsageList(view)).ShouldBeTrue("Tab reaches the usage list");
        }, Token).ConfigureAwait(true);
    }

    // Ordinal match: a cell shows exactly the text given, so "200" does not match "2000".
    private static bool Shows(TranscriptExportView view, string text) =>
        ExportHarness.UsageCells(view).Any(cell => string.Equals(cell, text, StringComparison.Ordinal));

    private static UiTree GoldenTranscript()
    {
        using var arguments = JsonDocument.Parse("{\"path\":\"a.txt\"}");
        return new UiTree
        {
            Blocks =
            [
                new UserMessageBlock { Id = "block-1", Text = "Hello" },
                new MarkdownBlock { Id = "block-2", Markdown = "Hi **there**" },
                new CodeBlock { Id = "block-3", Language = "csharp", Code = "Console.WriteLine(1);" },
                new ToolCallBlock
                {
                    Id = "block-4",
                    CallId = "call-1",
                    ToolName = "read",
                    Status = "done",
                    Arguments = arguments.RootElement.Clone(),
                },
            ],
            StreamingText = "partial answer",
        };
    }

    // Three runs on two days in one month: 2026-10-21 has two runs (200 tokens), 2026-10-22 has one (240), and the month
    // has three runs (440).
    private static UsageHistory SampleUsage() => new(
    [
        new UsageRecord("run-a", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), "model", 100, 20),
        new UsageRecord("run-b", new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), "model", 50, 30),
        new UsageRecord("run-c", new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero), "model", 200, 40),
    ]);

    private static UsageHistory EmptyUsage() => new([]);
}
