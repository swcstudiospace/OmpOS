using Autonogrammer.Native.Views.Cowork.Artifacts;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The Markdown and code artifact views (AG-71-10). A relative, <c>artifact:</c> or <c>javascript:</c> link is text, never an
/// anchor, and every other link shows its full URL without an anchor. Images are drawn only from inline raster data. Code is
/// highlighted only for an exact language tag. Neither view subscribes to a navigation event.
/// </summary>
public sealed class ArtifactRendererViewTests
{
    private const string LinkMarkdown =
        "[local](./notes.md) [artifact](artifact:other) [script](javascript:alert(1)) [web](https://example.com/x)";

    private const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    /// <summary>No link kind is an anchor, and each visible link shows its full target as text.</summary>
    [Fact]
    public async Task NoLinkKindBecomesAnAnchorAndTheFullTargetIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var view = new ArtifactMarkdownView();
            view.Show(LinkMarkdown);
            var window = Show(view);
            try
            {
                view.GetVisualDescendants().OfType<HyperlinkButton>().ShouldBeEmpty();
                view.GetVisualDescendants().OfType<Button>().ShouldBeEmpty();
                var text = string.Concat(view.GetVisualDescendants().OfType<TextBlock>().Select(PlainText));
                text.ShouldContain("./notes.md");
                text.ShouldContain("artifact:other");
                text.ShouldContain("javascript:alert(1)");
                text.ShouldContain("https://example.com/x");
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>A remote image is not drawn; an inline PNG data URI is drawn as one picture.</summary>
    [Fact]
    public async Task ImagesAreDrawnOnlyFromInlineRasterData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var remote = new ArtifactMarkdownView();
            remote.Show("![remote](https://example.com/a.png)");
            var remoteWindow = Show(remote);
            try
            {
                remote.GetVisualDescendants().OfType<Image>().ShouldBeEmpty();
            }
            finally
            {
                remoteWindow.Close();
            }

            var inline = new ArtifactMarkdownView();
            inline.Show("![pixel](data:image/png;base64," + Png1x1 + ")");
            var inlineWindow = Show(inline);
            try
            {
                inline.GetVisualDescendants().OfType<Image>().Count().ShouldBe(1);
            }
            finally
            {
                inlineWindow.Close();
            }
        }, ct);
    }

    /// <summary>Only the exact tags are highlighted; any other tag, and the case of a tag, is plain text.</summary>
    [Fact]
    public async Task OnlyExactLanguageTagsAreHighlighted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var code = new ArtifactCodeView();
            code.Show("unknown-lang", "x");
            code.HighlightScope.ShouldBeNull();
            code.Show("CSharp", "class A { }");
            code.HighlightScope.ShouldBeNull();
            code.Show(null, "plain");
            code.HighlightScope.ShouldBeNull();
            code.Show("csharp", "class A { }");
            code.HighlightScope.ShouldBe("source.cs");
            code.Text.ShouldBe("class A { }");
        }, ct);
    }

    /// <summary>Neither view file subscribes to or triggers a navigation, a new window or a link launch.</summary>
    [Fact]
    public void NeitherViewSubscribesToNavigation()
    {
        var directory = Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Views", "Cowork", "Artifacts");
        string[] forbidden = ["NavigationStarted", "NavigationCompleted", "NewWindowRequested", "NavigateUri", "Hyperlink", "PointerPressed", "Launcher", "Navigate("];
        foreach (var file in new[] { "ArtifactMarkdownView.axaml", "ArtifactMarkdownView.axaml.cs", "ArtifactCodeView.axaml", "ArtifactCodeView.axaml.cs" })
        {
            var text = File.ReadAllText(Path.Combine(directory, file));
            foreach (var token in forbidden)
            {
                text.ShouldNotContain(token, customMessage: file + " must not use " + token);
            }
        }
    }

    private static string PlainText(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines ? InlineText(inlines) : block.Text ?? string.Empty;

    private static string InlineText(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(static inline => inline switch
    {
        Run run => run.Text,
        Span span => InlineText(span.Inlines),
        LineBreak => "\n",
        _ => string.Empty,
    }));

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 900, Height = 1200 };
        window.Show();
        window.Activate();
        ShellHarness.Settle(window);
        return window;
    }
}
