using System.Text;
using System.Text.Json;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Presentation.Blocks;

namespace OmpOS.Presentation.Export;

/// <summary>
/// Exports a chat or run transcript to Markdown. It is a pure function of the transcript and the title, so the same
/// input always gives the same bytes.
/// </summary>
/// <remarks>
/// <para>
/// The output starts with a level-one heading holding the title. Each block follows, in transcript order, under a
/// level-two heading that names its kind, such as <c>## User</c>, <c>## Assistant</c> or <c>## Tool call: read</c>.
/// Each block's text follows its heading verbatim, so markdown in a message stays markdown in the export.
/// </para>
/// <para>
/// Tool calls, permission requests and plans are exported as their text: a tool call keeps its status, call id and raw
/// JSON arguments, and a permission request lists its options. The streaming tail, when present, is exported last as
/// an in-progress assistant section, because it is text the transcript has not yet folded into a block.
/// </para>
/// </remarks>
public static class TranscriptMarkdownExporter
{
    /// <summary>Exports the transcript to a Markdown document with the given title.</summary>
    /// <param name="transcript">The transcript whose blocks are exported in order.</param>
    /// <param name="title">The document title, written as the level-one heading. Line breaks are replaced with spaces.</param>
    /// <returns>The Markdown document. It ends with a single newline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transcript"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null, empty or white space.</exception>
    /// <exception cref="NotSupportedException">The transcript holds a block type this exporter does not know.</exception>
    public static string Export(UiTree transcript, string title)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var builder = new StringBuilder();
        builder.Append("# ").Append(OneLine(title)).Append('\n');

        foreach (var block in transcript.Blocks)
        {
            AppendSection(builder, BlockHeading(block), BlockBody(block));
        }

        if (transcript.StreamingText.Length > 0)
        {
            AppendSection(builder, "Assistant (in progress)", Normalize(transcript.StreamingText));
        }

        return builder.ToString();
    }

    private static string BlockHeading(UiBlock block) => block switch
    {
        UserMessageBlock => "User",
        MarkdownBlock => "Assistant",
        CodeBlock code => code.Language.Length == 0 ? "Code" : $"Code: {OneLine(code.Language)}",
        ToolCallBlock call => $"Tool call: {OneLine(call.ToolName)}",
        PermissionBlock => "Permission request",
        PlanBlock => "Plan",
        DiffPlaceholderBlock diff => $"Diff: {OneLine(diff.Path)}",
        ErrorBlock => "Error",
        A2uiSurfaceBlock surface => $"Interface: {OneLine(surface.SurfaceId)}",
        UnknownBlock unknown => unknown.OriginalKind is { Length: > 0 } kind
            ? $"Unknown block: {OneLine(kind)}"
            : "Unknown block",
        _ => throw new NotSupportedException($"Block type {block.GetType().Name} cannot be exported."),
    };

    private static string BlockBody(UiBlock block) => block switch
    {
        UserMessageBlock user => Normalize(user.Text),
        MarkdownBlock markdown => Normalize(markdown.Markdown),
        CodeBlock code => Fence(OneLine(code.Language), Normalize(code.Code)),
        ToolCallBlock call => ToolCallBody(call),
        PermissionBlock permission => PermissionBody(permission),
        PlanBlock plan => PlanBody(plan),
        DiffPlaceholderBlock diff => Normalize(diff.Reason),
        ErrorBlock error => ErrorBody(error),
        A2uiSurfaceBlock surface => A2uiBody(surface),
        UnknownBlock unknown => RawJson(unknown.Raw),
        _ => throw new NotSupportedException($"Block type {block.GetType().Name} cannot be exported."),
    };

    private static string ToolCallBody(ToolCallBlock call)
    {
        var lines = new List<string>
        {
            $"Status: {OneLine(call.Status)}",
            string.Empty,
            $"Call ID: {OneLine(call.CallId)}",
        };

        if (call.Arguments.ValueKind != JsonValueKind.Undefined)
        {
            lines.Add(string.Empty);
            lines.Add("Arguments:");
            lines.Add(string.Empty);
            lines.Add(Fence("json", call.Arguments.GetRawText()));
        }

        return string.Join('\n', lines);
    }

    private static string PermissionBody(PermissionBlock permission)
    {
        var lines = new List<string> { Normalize(permission.Prompt) };
        if (permission.Options.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(permission.Options.Select(static option => $"- {OneLine(option)}"));
        }

        return string.Join('\n', lines);
    }

    private static string PlanBody(PlanBlock plan) =>
        string.Join('\n', plan.Steps.Select(static (step, index) => $"{index + 1}. {OneLine(step)}"));

    private static string ErrorBody(ErrorBlock error) =>
        $"{Normalize(error.Message)}\n\nRetryable: {(error.Retryable ? "yes" : "no")}";

    private static string A2uiBody(A2uiSurfaceBlock surface)
    {
        var lines = new List<string>();
        foreach (var item in A2uiBlockMapping.Map(surface))
        {
            lines.Add(item switch
            {
                A2uiHeadingItem heading => $"**{Normalize(heading.Text ?? string.Empty)}**",
                A2uiParagraphItem paragraph => Normalize(paragraph.Text ?? string.Empty),
                A2uiButtonItem button => $"[{OneLine(button.Label ?? string.Empty)}]",
                A2uiListItem list => string.Join('\n', list.Items.Select(static text => $"- {OneLine(text)}")),
                A2uiImageItem image => $"Image: {OneLine(image.Alt ?? string.Empty)}",
                A2uiImageFallbackItem fallback => $"Image: {OneLine(fallback.Text ?? string.Empty)}",
                A2uiUnsupportedItem unsupported => $"Unsupported component: {OneLine(unsupported.Type)}",
                _ => throw new NotSupportedException($"A2UI item {item.GetType().Name} cannot be exported."),
            });
        }

        return string.Join("\n\n", lines);
    }

    private static string RawJson(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Undefined ? string.Empty : Fence("json", raw.GetRawText());

    /// <summary>
    /// Writes a level-two heading and its body. A block with no body is written as its heading alone.
    /// </summary>
    private static void AppendSection(StringBuilder builder, string heading, string body)
    {
        builder.Append('\n').Append("## ").Append(heading).Append('\n');
        if (body.Length > 0)
        {
            builder.Append('\n').Append(body).Append('\n');
        }
    }

    /// <summary>
    /// Wraps content in a backtick fence longer than any backtick run inside it, so the content cannot close the fence.
    /// </summary>
    private static string Fence(string info, string content)
    {
        var longest = 0;
        var run = 0;
        foreach (var character in content)
        {
            run = character == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        var fence = new string('`', Math.Max(3, longest + 1));
        return $"{fence}{info}\n{content}\n{fence}";
    }

    /// <summary>Normalises line endings to LF and removes trailing line breaks, so a block never adds blank lines.</summary>
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>Replaces line breaks with spaces so a single-line field cannot break its heading or list item.</summary>
    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();
}
