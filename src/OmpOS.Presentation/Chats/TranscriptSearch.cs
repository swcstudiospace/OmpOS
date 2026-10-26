using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Presentation.Blocks;

namespace OmpOS.Presentation.Chats;

/// <summary>
/// Case-insensitive search over a session transcript. It is a pure function of the transcript and the query, so the same
/// transcript and query always give the same matches.
/// </summary>
/// <remarks>
/// <para>
/// Matches are ordered by block, then by offset. Within one block, matches do not overlap: the scan resumes after each
/// match, as an editor's find does, so <c>"aa"</c> in <c>"aaaa"</c> matches twice.
/// </para>
/// <para>
/// A block's searchable text is the text it shows: markdown source, code, the user's prompt, the tool name, the permission
/// prompt and options, plan steps, the diff path and reason, the error message, and the text of supported A2UI components.
/// Multi-field blocks are searched as one text with newline separators, so a match never spans two fields.
/// Unknown blocks are not searched: their raw JSON is not transcript text.
/// </para>
/// </remarks>
public static class TranscriptSearch
{
    /// <summary>Finds every case-insensitive match of <paramref name="query"/> in the transcript, in order.</summary>
    /// <param name="transcript">The transcript to search, with its streaming tail.</param>
    /// <param name="query">The text to find. An empty query matches nothing.</param>
    /// <returns>The matches in transcript order. Empty when nothing matches or the query is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transcript"/> or <paramref name="query"/> is null.</exception>
    public static IReadOnlyList<TranscriptMatch> Find(UiTree transcript, string query)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(query);

        var matches = new List<TranscriptMatch>();
        if (query.Length == 0)
        {
            return matches;
        }

        for (var index = 0; index < transcript.Blocks.Count; index++)
        {
            AddMatches(matches, index, BlockText(transcript.Blocks[index]), query);
        }

        // The streaming tail sits after the last block; its index is the block count.
        AddMatches(matches, transcript.Blocks.Count, transcript.StreamingText, query);
        return matches;
    }

    /// <summary>Appends the non-overlapping case-insensitive matches of <paramref name="query"/> in one text.</summary>
    internal static void AddMatches(List<TranscriptMatch> matches, int blockIndex, string text, string query)
    {
        var start = 0;
        while (start <= text.Length - query.Length)
        {
            var found = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return;
            }

            matches.Add(new TranscriptMatch(blockIndex, found, query.Length));
            start = found + query.Length;
        }
    }

    private static string BlockText(UiBlock block) => block switch
    {
        MarkdownBlock markdown => markdown.Markdown,
        CodeBlock code => code.Code,
        UserMessageBlock user => user.Text,
        ToolCallBlock call => call.ToolName,
        PermissionBlock permission => Join([permission.Prompt, .. permission.Options]),
        PlanBlock plan => Join(plan.Steps),
        DiffPlaceholderBlock diff => Join([diff.Path, diff.Reason]),
        ErrorBlock error => error.Message,
        A2uiSurfaceBlock surface => A2uiText(surface),
        _ => string.Empty,
    };

    private static string A2uiText(A2uiSurfaceBlock surface)
    {
        var parts = new List<string>();
        foreach (var item in A2uiBlockMapping.Map(surface))
        {
            switch (item)
            {
                case A2uiHeadingItem heading:
                    parts.Add(heading.Text ?? string.Empty);
                    break;
                case A2uiParagraphItem paragraph:
                    parts.Add(paragraph.Text ?? string.Empty);
                    break;
                case A2uiButtonItem button:
                    parts.Add(button.Label ?? string.Empty);
                    break;
                case A2uiListItem list:
                    parts.AddRange(list.Items);
                    break;
                case A2uiImageItem image:
                    parts.Add(image.Alt ?? string.Empty);
                    break;
                case A2uiImageFallbackItem fallback:
                    parts.Add(fallback.Text ?? string.Empty);
                    break;
            }
        }

        return Join(parts);
    }

    private static string Join(IEnumerable<string> parts) => string.Join('\n', parts);
}
