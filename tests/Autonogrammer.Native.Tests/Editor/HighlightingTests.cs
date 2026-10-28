using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using Autonogrammer.Native.Views.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Editor;

/// <summary>
/// TextMate highlighting for C#, TypeScript, JSON, YAML and Markdown (ADE-09). Each language is checked on the rendered
/// text runs of the editor: at least one run must carry a foreground that differs from the editor's default foreground.
/// </summary>
public sealed class HighlightingTests
{
    /// <summary>Each language's keyword fixture is highlighted with at least one non-default foreground token.</summary>
    [Theory]
    [InlineData("src/Sample.cs", "using System;\npublic class Sample\n{\n    public static void Main() { }\n}\n")]
    [InlineData("src/sample.ts", "export const greeting: string = 'hi';\nfunction greet(name: string): string { return name; }\n")]
    [InlineData("config/sample.json", "{\n  \"name\": \"autonogrammer\",\n  \"enabled\": true\n}\n")]
    [InlineData("config/sample.yaml", "name: autonogrammer\nenabled: true\nitems:\n  - one\n")]
    [InlineData("docs/sample.md", "# Title\n\nSome *emphasis* and `code`.\n")]
    public async Task EachLanguageHighlightsAtLeastOneNonDefaultToken(string path, string text)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(path, text);
            using var window = EditorHarness.Show(model);

            var (tokens, detail) = CountNonDefaultTokens(window);
            tokens.ShouldBeGreaterThan(0, $"no non-default token for {path}: {detail}");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Switching tabs switches the grammar, so the newly selected file is highlighted too.</summary>
    [Fact]
    public async Task TokensFollowTheSelectedTabAcrossSwitches()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open("docs/sample.md", "# Title\n\nSome *emphasis*.\n");
            model.Open("config/sample.json", "{\n  \"enabled\": true\n}\n");
            using var window = EditorHarness.Show(model);

            CountNonDefaultTokens(window).Tokens.ShouldBeGreaterThan(0, "the JSON tab is highlighted when selected");

            EditorHarness.Click(window, EditorHarness.TabToggle(window, "sample.md"));

            CountNonDefaultTokens(window).Tokens.ShouldBeGreaterThan(0, "the Markdown tab is highlighted after the switch");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each workspace extension named by ADE-09 maps to one grammar scope.</summary>
    [Theory]
    [InlineData("src/Program.cs", "source.cs")]
    [InlineData("src/app.ts", "source.ts")]
    [InlineData("config/app.json", "source.json")]
    [InlineData("config/app.yaml", "source.yaml")]
    [InlineData("config/app.yml", "source.yaml")]
    [InlineData("docs/README.md", "text.html.markdown")]
    [InlineData("docs/notes.markdown", "text.html.markdown")]
    public void ExtensionMapsToItsGrammarScope(string path, string scope) =>
        EditorLanguages.ScopeFor(path).ShouldBe(scope);

    /// <summary>A file with no grammar in the set is shown as plain text.</summary>
    [Fact]
    public void UnknownExtensionHasNoGrammarScope()
    {
        EditorLanguages.ScopeFor("notes/readme.txt").ShouldBeNull();
        EditorLanguages.ScopeFor(".gitignore").ShouldBeNull();
    }

    private static readonly TimeSpan TokenWaitLimit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TokenPollInterval = TimeSpan.FromMilliseconds(20);

    // Counts the text runs of the editor's visible lines whose foreground differs from the editor's default foreground. The
    // TextMate tokenizes in the background and redraws each line when its tokens arrive, so the count is taken again, with
    // dispatcher and layout work between the takes, until a non-default token appears or the wait limit passes.
    private static (int Tokens, string Detail) CountNonDefaultTokens(Window window)
    {
        var editor = EditorHarness.Code(window);
        var clock = Stopwatch.StartNew();
        var tokens = 0;
        while (tokens == 0 && clock.Elapsed < TokenWaitLimit)
        {
            EditorHarness.Settle(window);
            tokens = CountOnce(editor);
            if (tokens == 0)
            {
                Thread.Sleep(TokenPollInterval);
            }
        }

        return (tokens, Describe(editor));
    }

    private static int CountOnce(TextEditor editor)
    {
        var defaultColour = (editor.Foreground as ISolidColorBrush)?.Color;
        editor.TextArea.TextView.EnsureVisualLines();

        return editor.TextArea.TextView.VisualLines
            .SelectMany(line => line.TextLines)
            .SelectMany(textLine => textLine.TextRuns)
            .Count(run => run.Properties?.ForegroundBrush is ISolidColorBrush brush && brush.Color != defaultColour);
    }

    private static string Describe(TextEditor editor)
    {
        var lines = editor.TextArea.TextView.VisualLines;
        var runs = lines.SelectMany(line => line.TextLines).SelectMany(textLine => textLine.TextRuns).ToList();
        return $"visual lines {lines.Count}, text runs {runs.Count}, shaped runs {runs.OfType<ShapedTextRun>().Count()}, " +
            $"editor foreground {editor.Foreground}, transformers {editor.TextArea.TextView.LineTransformers.Count}";
    }
}
