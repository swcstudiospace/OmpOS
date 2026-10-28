using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Blocks;

/// <summary>
/// One headless render check per transcript block kind: the native half of the web head's <c>UiBlockRendererTests</c>.
/// The transcript's reaction to the view model's PropertyChanged is checked too. Every assertion reads what the user sees.
/// </summary>
public sealed class NativeUiBlockRendererTests
{
    /// <summary>A valid one-pixel PNG, so an inline image really decodes.</summary>
    private const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static readonly bool[] RetryableValues = [true, false];

    /// <summary>A markdown block renders emphasis as inlines, a link as its text and URL, and raw HTML as plain text.</summary>
    [Fact]
    public async Task RendersMarkdownText()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(ViewMapping.CreateBlock(new MarkdownBlock
            {
                Id = "m-1",
                Markdown = "hello **world**\n\n[site](https://example.com)\n\n<b>raw</b>",
            }));

            var paragraphs = host.Window.GetVisualDescendants().OfType<TextBlock>().ToList();
            paragraphs.Select(TextOf).ShouldBe(["hello world", "site (https://example.com)", "<b>raw</b>"]);

            var bold = Children(paragraphs[0]).OfType<Span>().Single();
            bold.FontWeight.ShouldBe(FontWeight.Bold);
            InlineText(Children(bold)).ShouldBe("world");

            host.Window.GetVisualDescendants().OfType<Button>().ShouldBeEmpty("a link is text, not a control");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A code block renders its language and code, with a copy control that writes the code to the clipboard.</summary>
    [Fact]
    public async Task RendersCodeBlockWithCopyControl()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var host = new Host(ViewMapping.CreateBlock(new CodeBlock { Id = "c-1", Language = "csharp", Code = "var x = 1;" }));

            var copy = host.Window.GetVisualDescendants().OfType<Button>().Single();
            AutomationProperties.GetName(copy).ShouldBe("Copy code");
            var texts = TextsOf(host.Window);
            texts.ShouldContain("csharp", StringComparer.Ordinal);
            texts.ShouldContain("var x = 1;", StringComparer.Ordinal);

            var clipboard = TopLevel.GetTopLevel(host.Window)?.Clipboard
                ?? throw new InvalidOperationException("The headless window has no clipboard.");
            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            (await clipboard.TryGetTextAsync().ConfigureAwait(true)).ShouldBe("var x = 1;");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A tool-call card renders the tool name and its status.</summary>
    [Fact]
    public async Task RendersToolCallCardWithStatus()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var arguments = JsonDocument.Parse("{\"path\":\"a.txt\"}");
            using var host = new Host(ViewMapping.CreateBlock(new ToolCallBlock
            {
                Id = "call-1",
                CallId = "call-1",
                ToolName = "read",
                Status = "completed",
                Arguments = arguments.RootElement.Clone(),
            }));

            var texts = TextsOf(host.Window);
            texts.ShouldContain("read", StringComparer.Ordinal);
            texts.ShouldContain("completed", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Permission options render verbatim, in agent-provided order, with no decision controls and the policy note.</summary>
    [Fact]
    public async Task RendersPermissionOptionsVerbatimReadOnly()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(ViewMapping.CreateBlock(new PermissionBlock
            {
                Id = "p-1",
                Prompt = "Run bash?",
                Options = ["allow", "deny", "allow always"],
            }));

            TextsOf(host.Window).ShouldContain("Run bash?", StringComparer.Ordinal);
            WithClass(host.Window, "permission-option").ShouldBe(["allow", "deny", "allow always"]);
            host.Window.GetVisualDescendants().OfType<Button>().ShouldBeEmpty("the options are read-only");
            TextsOf(host.Window).ShouldContain("The decision is made by policy.", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A plan panel renders its steps as a numbered list, in order.</summary>
    [Fact]
    public async Task RendersPlanPanel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(ViewMapping.CreateBlock(new PlanBlock { Id = "plan-1", Steps = ["read", "answer"] }));

            WithClass(host.Window, "plan-step").ShouldBe(["read", "answer"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A diff placeholder renders its path and reason.</summary>
    [Fact]
    public async Task RendersDiffPlaceholder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(ViewMapping.CreateBlock(new DiffPlaceholderBlock
            {
                Id = "d-1",
                Path = "src/Foo.cs",
                Reason = "large diff withheld",
            }));

            var texts = TextsOf(host.Window);
            texts.ShouldContain("src/Foo.cs", StringComparer.Ordinal);
            texts.ShouldContain("large diff withheld", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An error block renders its message and offers no action, whether or not the error is retryable.</summary>
    [Fact]
    public async Task RendersErrorBlock()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            foreach (var retryable in RetryableValues)
            {
                using var host = new Host(ViewMapping.CreateBlock(new ErrorBlock { Id = "e-1", Message = "boom", Retryable = retryable }));

                TextsOf(host.Window).ShouldContain("boom", StringComparer.Ordinal);
                host.Window.GetVisualDescendants().OfType<Button>().ShouldBeEmpty("the error block offers no action");
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The A2UI subset renders heading, text, an inline image, a button without action, and a list.</summary>
    [Fact]
    public async Task RendersA2uiSubset()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var payload = JsonDocument.Parse($$"""
                {"components": [
                  {"type": "heading", "text": "Title"},
                  {"type": "text", "text": "Body"},
                  {"type": "image", "url": "data:image/png;base64,{{OnePixelPng}}", "alt": "An image"},
                  {"type": "button", "label": "Press"},
                  {"type": "list", "items": ["a", "b"]}
                ]}
                """);
            using var host = new Host(ViewMapping.CreateBlock(new A2uiSurfaceBlock
            {
                Id = "a-1",
                SurfaceId = "s-1",
                Payload = payload.RootElement.Clone(),
            }));

            var texts = TextsOf(host.Window);
            texts.ShouldContain("Title", StringComparer.Ordinal);
            texts.ShouldContain("Body", StringComparer.Ordinal);
            texts.ShouldContain("Press", StringComparer.Ordinal);

            var picture = host.Window.GetVisualDescendants().OfType<Image>().Single();
            AutomationProperties.GetName(picture).ShouldBe("An image");
            picture.Source.ShouldNotBeNull();

            WithClass(host.Window, "a2ui-list-item").ShouldBe(["a", "b"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A remote A2UI image draws no image: a fallback link shows its alt text and its URL, and is not followed.</summary>
    [Fact]
    public async Task RemoteA2uiImageRendersFallbackLink()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var payload = JsonDocument.Parse("""
                {"components": [
                  {"type": "image", "url": "https://example.com/i.png", "alt": "An image"}
                ]}
                """);
            using var host = new Host(ViewMapping.CreateBlock(new A2uiSurfaceBlock
            {
                Id = "a-1",
                SurfaceId = "s-1",
                Payload = payload.RootElement.Clone(),
            }));

            host.Window.GetVisualDescendants().OfType<Image>().ShouldBeEmpty();
            host.Window.GetVisualDescendants().OfType<Button>().ShouldBeEmpty();
            var link = WithClassControls(host.Window, "a2ui-image-link").Single();
            TextOf(link).ShouldBe("An image", "the visible text is the link text only, as on the web head");
            ((string?)ToolTip.GetTip(link)).ShouldBe("https://example.com/i.png", "the destination is offered as the tooltip");
            AutomationProperties.GetHelpText(link).ShouldBe("https://example.com/i.png", "the destination is the accessible help text");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An inline A2UI image whose payload omits alt, or carries only white space, still gets the shared fallback name.</summary>
    [Fact]
    public async Task A2uiImageWithoutAltGetsTheFallbackName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var payload = JsonDocument.Parse($$"""
                {"components": [
                  {"type": "image", "url": "data:image/png;base64,{{OnePixelPng}}"},
                  {"type": "image", "url": "data:image/png;base64,{{OnePixelPng}}", "alt": "  "}
                ]}
                """);
            using var host = new Host(ViewMapping.CreateBlock(new A2uiSurfaceBlock
            {
                Id = "a-1",
                SurfaceId = "s-1",
                Payload = payload.RootElement.Clone(),
            }));

            var pictures = host.Window.GetVisualDescendants().OfType<Image>().ToList();
            pictures.Count.ShouldBe(2);
            pictures.Select(static picture => AutomationProperties.GetName(picture))
                .ShouldBe([Autonogrammer.GenUi.A2uiSurfaceCatalog.DefaultImageAlt, Autonogrammer.GenUi.A2uiSurfaceCatalog.DefaultImageAlt]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An allowed inline image whose bytes are not an image draws no picture: it falls back to its alt text, and the transcript keeps rendering.</summary>
    [Fact]
    public async Task UndecodableInlineImageFallsBackToItsAltText()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            // A truncated PNG: the data URI is allowed, but the bytes are not an image.
            using var payload = JsonDocument.Parse("""
                {"components": [
                  {"type": "image", "url": "data:image/png;base64,iVBORw0KGgo=", "alt": "Broken picture"}
                ]}
                """);
            using var host = new Host(ViewMapping.CreateBlock(new A2uiSurfaceBlock
            {
                Id = "a-1",
                SurfaceId = "s-1",
                Payload = payload.RootElement.Clone(),
            }));

            host.Window.GetVisualDescendants().OfType<Image>().ShouldBeEmpty();
            TextsOf(host.Window).ShouldContain("Broken picture", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An unknown block renders as a collapsed disclosure that names its kind and shows the raw JSON.</summary>
    [Fact]
    public async Task RendersUnknownCollapsed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var raw = JsonDocument.Parse("{\"kind\":\"future\",\"n\":1}");
            using var host = new Host(ViewMapping.CreateBlock(new UnknownBlock
            {
                Id = "u-1",
                OriginalKind = "future",
                Raw = raw.RootElement.Clone(),
            }));

            var disclosure = host.Window.GetVisualDescendants().OfType<Expander>().Single();
            disclosure.IsExpanded.ShouldBeFalse();
            ((string?)disclosure.Header).ShouldBe("Unsupported content (future)");
            TextOf(Find(disclosure.Content as TextBlock, "the disclosure content")).ShouldContain("future");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A user turn renders its prompt verbatim as plain text, never as markdown.</summary>
    [Fact]
    public async Task RendersUserMessage()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(ViewMapping.CreateBlock(new UserMessageBlock { Id = "u-1", Text = "hi *there*" }));

            // Parsed as markdown, the emphasis would vanish from the text; verbatim, the text is exactly the prompt.
            TextsOf(host.Window).ShouldContain("hi *there*", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// The transcript draws the folded tree and redraws when the model raises PropertyChanged: a streaming delta shows as
    /// plain text, and the next block flushes it into the transcript, with no refresh call.
    /// </summary>
    [Fact]
    public async Task TranscriptFollowsTreeThroughPropertyChanged()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.ApplyEvent(Stream(new UiBlockPayload { Block = new PlanBlock { Id = "plan-1", Steps = ["first step"] } }));
            var view = (SessionView)ViewMapping.Create(model);
            using var host = new Host(view);
            TextsOf(host.Window).ShouldContain("first step", StringComparer.Ordinal);

            model.ApplyEvent(Stream(new TextDeltaPayload { Delta = "streamed words" }));
            var streaming = Find(view.FindControl<TextBlock>("StreamingText"), "the streaming text");
            streaming.IsVisible.ShouldBeTrue();
            streaming.Text.ShouldBe("streamed words");
            var announcement = Find(view.FindControl<TextBlock>("AnnouncementText"), "the announcement");
            model.Announcement.ShouldNotBeNullOrWhiteSpace();
            announcement.Text.ShouldBe(model.Announcement);

            model.ApplyEvent(Stream(new UiBlockPayload
            {
                Block = new DiffPlaceholderBlock { Id = "d-1", Path = "src/Foo.cs", Reason = "large diff withheld" },
            }));
            // A user-control presents its content through its template, which is applied on the window's layout pass.
            ShellHarness.Settle(host.Window);
            var texts = TextsOf(host.Window);
            texts.ShouldContain("first step", StringComparer.Ordinal);
            texts.ShouldContain("src/Foo.cs", StringComparer.Ordinal);
            texts.ShouldContain("streamed words", StringComparer.Ordinal);
            streaming.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = "s-1",
        CorrelationId = "s-1/turn-1",
        Timestamp = Stamp,
        Payload = payload,
    };

    private static T Find<T>(T? control, string description) where T : class =>
        control ?? throw new InvalidOperationException($"{description} is not in the view.");

    private static IEnumerable<Inline> Children(TextBlock block) => block.Inlines ?? Enumerable.Empty<Inline>();

    private static IEnumerable<Inline> Children(Span span) => span.Inlines ?? Enumerable.Empty<Inline>();

    private static string TextOf(TextBlock block) => block.Text ?? InlineText(Children(block));

    private static string InlineText(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(static inline => inline switch
    {
        Run run => run.Text,
        Span span => InlineText(Children(span)),
        LineBreak => "\n",
        _ => string.Empty,
    }));

    private static List<string> TextsOf(Visual root)
    {
        var texts = root.GetVisualDescendants().OfType<TextBlock>().Select(TextOf).ToList();
        texts.AddRange(root.GetVisualDescendants().OfType<Button>().Select(static button => button.Content as string).OfType<string>());
        return texts;
    }

    private static List<TextBlock> WithClassControls(Visual root, string cssClass) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains(cssClass)).ToList();

    private static List<string> WithClass(Visual root, string cssClass) => WithClassControls(root, cssClass).Select(TextOf).ToList();

    /// <summary>A shown window that hosts one block view, closed when the test disposes it.</summary>
    private sealed class Host : IDisposable
    {
        public Host(Control content)
        {
            Window = new Window { Content = content, Width = 640, Height = 480 };
            Window.Show();
            ShellHarness.Settle(Window);
        }

        public Window Window { get; }

        public void Dispose() => Window.Close();
    }
}
