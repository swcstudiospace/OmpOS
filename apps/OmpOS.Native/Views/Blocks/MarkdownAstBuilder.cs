using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Inline = Avalonia.Controls.Documents.Inline;
using MarkdownInline = Markdig.Syntax.Inlines.Inline;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// Builds Avalonia controls from the Markdig syntax tree of one markdown block. The pipeline matches
/// <c>MarkdownRenderer</c>: raw HTML is disabled and the advanced extensions are on. Nothing navigates. A link shows its
/// text and its URL; an image is drawn only when it is an allowed inline data URI, and otherwise shows its text.
/// </summary>
internal static class MarkdownAstBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>Parses markdown and builds one control per top-level block, in source order.</summary>
    /// <param name="markdown">The markdown source.</param>
    /// <returns>The controls, in source order. A block with nothing to show yields no control.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is null.</exception>
    public static List<Control> Build(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var controls = new List<Control>();
        foreach (var block in Markdown.Parse(markdown, Pipeline))
        {
            var control = BlockControl(block);
            if (control is not null)
            {
                controls.Add(control);
            }
        }

        return controls;
    }

    private static Control? BlockControl(Block block) => block switch
    {
        HeadingBlock heading => BlockControls.AsHeading(Paragraph(heading.Inline)),
        ParagraphBlock paragraph => Paragraph(paragraph.Inline),
        FencedCodeBlock fenced => CodeControl(fenced.Lines.ToString()),
        CodeBlock code => CodeControl(code.Lines.ToString()),
        HtmlBlock html => BlockControls.Text(html.Lines.ToString().TrimEnd('\n')),
        QuoteBlock quote => QuoteControl(quote),
        ListBlock list => ListControl(list),
        Table table => TableControl(table),
        ThematicBreakBlock => RuleControl(),
        LinkReferenceDefinitionGroup => null,
        ContainerBlock container => Stack(container),
        _ => null,
    };

    private static StackPanel Stack(IEnumerable<Block> blocks)
    {
        var panel = new StackPanel { Spacing = BlockTokens.Get<double>("OmpOS.Gap.Sm") };
        foreach (var block in blocks)
        {
            var control = BlockControl(block);
            if (control is not null)
            {
                panel.Children.Add(control);
            }
        }

        return panel;
    }

    private static TextBlock Paragraph(ContainerInline? inlines)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AppendInlines(BlockControls.InlinesOf(text), inlines);
        return text;
    }

    private static Border CodeControl(string code) => new()
    {
        Background = BlockTokens.Get<IBrush>("OmpOS.Color.Surface"),
        Padding = BlockTokens.Get<Thickness>("OmpOS.Space.Sm"),
        Child = new TextBlock
        {
            Text = code.TrimEnd('\n'),
            FontFamily = BlockTokens.Monospace,
            TextWrapping = TextWrapping.Wrap,
        },
    };

    private static Border QuoteControl(QuoteBlock quote) => new()
    {
        BorderBrush = BlockTokens.Get<IBrush>("OmpOS.Color.TextMuted"),
        BorderThickness = new Thickness(2, 0, 0, 0),
        Padding = BlockTokens.Get<Thickness>("OmpOS.Space.Sm"),
        Child = Stack(quote),
    };

    private static StackPanel ListControl(ListBlock list)
    {
        var panel = new StackPanel { Spacing = BlockTokens.Get<double>("OmpOS.Gap.Xs") };
        var number = int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? $"{number}." : "•";
            number++;
            panel.Children.Add(BlockControls.Row(marker, Stack(item)));
        }

        return panel;
    }

    private static Grid TableControl(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        var columns = Math.Max(1, rows.Count == 0 ? 0 : rows.Max(static row => row.OfType<TableCell>().Count()));
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", columns))),
            ColumnSpacing = BlockTokens.Get<double>("OmpOS.Gap.Sm"),
            RowSpacing = BlockTokens.Get<double>("OmpOS.Gap.Xs"),
        };

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cells = rows[rowIndex].OfType<TableCell>().ToList();
            for (var column = 0; column < cells.Count; column++)
            {
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
                AppendCellInlines(BlockControls.InlinesOf(text), cells[column]);
                if (rows[rowIndex].IsHeader)
                {
                    text.FontWeight = BlockTokens.Get<FontWeight>("OmpOS.Font.Weight.Semibold");
                }

                Grid.SetRow(text, rowIndex);
                Grid.SetColumn(text, column);
                grid.Children.Add(text);
            }
        }

        return grid;
    }

    private static Border RuleControl() => new()
    {
        Height = 1,
        Background = BlockTokens.Get<IBrush>("OmpOS.Color.SurfaceActive"),
        Margin = BlockTokens.Get<Thickness>("OmpOS.Space.Sm"),
    };

    private static void AppendCellInlines(InlineCollection target, TableCell cell)
    {
        foreach (var paragraph in cell.OfType<ParagraphBlock>())
        {
            if (target.Count > 0)
            {
                target.Add(new Run(" "));
            }

            AppendInlines(target, paragraph.Inline);
        }
    }

    private static void AppendInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(target, inline);
        }
    }

    private static void AppendInline(InlineCollection target, MarkdownInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run(literal.Content.ToString()));
                break;
            case EmphasisInline emphasis:
                target.Add(EmphasisSpan(emphasis));
                break;
            case CodeInline code:
                target.Add(new Run(code.Content) { FontFamily = BlockTokens.Monospace });
                break;
            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    target.Add(new LineBreak());
                }
                else
                {
                    target.Add(new Run(" "));
                }

                break;
            case LinkInline image when image.IsImage:
                AppendImage(target, image);
                break;
            case LinkInline link:
                AppendLink(target, link);
                break;
            case HtmlInline html:
                target.Add(new Run(html.Tag));
                break;
            case HtmlEntityInline entity:
                target.Add(new Run(entity.Transcoded.ToString()));
                break;
            case TaskList task:
                target.Add(new Run(task.Checked ? "☑ " : "☐ "));
                break;
            case ContainerInline container:
                {
                    var span = new Span();
                    AppendInlines(SpanInlines(span), container);
                    target.Add(span);
                    break;
                }
            default:
                break;
        }
    }

    private static Span EmphasisSpan(EmphasisInline emphasis)
    {
        var span = new Span();
        AppendInlines(SpanInlines(span), emphasis);
        if (emphasis.DelimiterChar == '~')
        {
            span.TextDecorations = BlockControls.Strikethrough();
            return span;
        }

        if (emphasis.DelimiterCount >= 2)
        {
            span.FontWeight = FontWeight.Bold;
        }

        if (emphasis.DelimiterCount % 2 == 1)
        {
            span.FontStyle = FontStyle.Italic;
        }

        return span;
    }

    private static void AppendLink(InlineCollection target, LinkInline link)
    {
        var label = new Span();
        var labelInlines = SpanInlines(label);
        AppendInlines(labelInlines, link);
        var url = link.Url;

        // No target, or an in-document anchor such as a footnote: the text alone, since there is nothing to show as a link.
        if (string.IsNullOrEmpty(url) || url.StartsWith('#'))
        {
            target.Add(label);
            return;
        }

        label.TextDecorations = BlockControls.Underline();
        target.Add(label);

        // An autolink already shows its URL as its text, and so does a link whose text is its own URL.
        if (!link.IsAutoLink && !string.Equals(PlainText(labelInlines), url, StringComparison.Ordinal))
        {
            target.Add(new Run(" (" + url + ")"));
        }
    }

    private static void AppendImage(InlineCollection target, LinkInline image)
    {
        var alt = new Span();
        var altInlines = SpanInlines(alt);
        AppendInlines(altInlines, image);
        var altText = PlainText(altInlines);
        var source = image.Url ?? string.Empty;

        var picture = DataImage.TryCreatePicture(source);
        if (picture is not null)
        {
            AutomationProperties.SetName(picture, altText.Length > 0 ? altText : "Image");
            target.Add(new InlineUIContainer { Child = picture });
            return;
        }

        // Not an inline image this view can draw: its text, and the source when the source is not an inline data URI.
        var label = altText.Length > 0 ? altText : source;
        if (label.Length == 0)
        {
            return;
        }

        target.Add(new Run(label) { TextDecorations = BlockControls.Underline() });
        if (source.Length > 0 && !source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && !string.Equals(label, source, StringComparison.Ordinal))
        {
            target.Add(new Run(" (" + source + ")"));
        }
    }

    private static InlineCollection SpanInlines(Span span) =>
        span.Inlines ?? throw new InvalidOperationException("A span always has an inline collection.");

    private static string PlainText(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(static inline => inline switch
    {
        Run run => run.Text,
        Span span => PlainText(SpanInlines(span)),
        LineBreak => "\n",
        _ => string.Empty,
    }));
}
