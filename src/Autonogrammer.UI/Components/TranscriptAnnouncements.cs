using System.Globalization;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components.Blocks;

namespace Autonogrammer.UI.Components;

/// <summary>
/// The sentences the transcript's single polite status region speaks, and the rule that finds them. A sentence is spoken when an
/// event completes (the assistant finished, a tool finished, an approval is needed), never for each streamed text delta, so a screen
/// reader user hears what happened without the transcript text being read out as it arrives. The facts come from the blocks the
/// transcript already holds.
/// </summary>
public static class TranscriptAnnouncements
{
    /// <summary>The sentence spoken after a code block was copied to the clipboard.</summary>
    public const string CopiedToClipboard = "Copied to clipboard";

    /// <summary>Finds the events that completed between two states of the transcript.</summary>
    /// <param name="previousTree">The tree the transcript showed before, or null for the first one.</param>
    /// <param name="previousState">The state the transcript was in before, or null for the first one.</param>
    /// <param name="tree">The tree the transcript shows now.</param>
    /// <param name="state">The state the transcript is in now.</param>
    /// <returns>The sentences of the completed events joined by a space, or null when none completed. A history that was just loaded and a replacement transcript announce nothing.</returns>
    public static string? Completed(UiTree? previousTree, SessionViewState? previousState, UiTree tree, SessionViewState state)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (previousTree is null || previousState is null or SessionViewState.LoadingHistory)
        {
            return null;
        }

        var before = previousTree.Blocks;
        var after = tree.Blocks;
        if (after.Count < before.Count)
        {
            return null;
        }

        List<string>? sentences = null;
        if (!ReferenceEquals(before, after))
        {
            for (var i = 0; i < after.Count; i++)
            {
                var sentence = i >= before.Count ? OfNewBlock(after[i]) : OfChangedBlock(before[i], after[i]);
                if (sentence is not null)
                {
                    (sentences ??= []).Add(sentence);
                }
            }
        }

        if (previousState == SessionViewState.Streaming && state == SessionViewState.Success)
        {
            (sentences ??= []).Add(AssistantFinished(CountWordsOfLastTurn(tree)));
        }

        return sentences is null ? null : string.Join(' ', sentences);
    }

    /// <summary>Words the assistant finished with.</summary>
    /// <param name="words">The number of words the assistant wrote in its last turn.</param>
    /// <returns>The sentence to speak.</returns>
    public static string AssistantFinished(int words) => words switch
    {
        0 => "Assistant finished",
        1 => "Assistant finished: 1 word",
        _ => string.Create(CultureInfo.InvariantCulture, $"Assistant finished: {words} words"),
    };

    /// <summary>Words that tell how many earlier messages "Show earlier messages" revealed.</summary>
    /// <param name="turns">The number of earlier messages that were added to the transcript.</param>
    /// <returns>The sentence to speak.</returns>
    public static string EarlierShown(int turns) => turns == 1
        ? "1 earlier message shown"
        : string.Create(CultureInfo.InvariantCulture, $"{turns} earlier messages shown");

    private static string? OfNewBlock(UiBlock block) => block is PermissionBlock { Decision: null } permission
        ? $"Approval needed: {permission.Prompt}"
        : null;

    private static string? OfChangedBlock(UiBlock before, UiBlock after)
    {
        if (ReferenceEquals(before, after) || before is not ToolCallBlock was || after is not ToolCallBlock now)
        {
            return null;
        }

        if (!string.Equals(was.CallId, now.CallId, StringComparison.Ordinal) || string.Equals(was.Status, now.Status, StringComparison.Ordinal))
        {
            return null;
        }

        return ToolStatusIcon.Of(now.Status) switch
        {
            StatusIcon.Check => $"Tool {now.ToolName} finished",
            StatusIcon.Cross => $"Tool {now.ToolName} failed",
            _ => null,
        };
    }

    private static int CountWordsOfLastTurn(UiTree tree)
    {
        var blocks = tree.Blocks;
        var start = 0;
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            if (blocks[i] is UserMessageBlock)
            {
                start = i + 1;
                break;
            }
        }

        var words = CountWords(tree.StreamingText);
        for (var i = start; i < blocks.Count; i++)
        {
            if (blocks[i] is MarkdownBlock markdown)
            {
                words += CountWords(markdown.Markdown);
            }
        }

        return words;
    }

    private static int CountWords(string text)
    {
        var words = 0;
        var inWord = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }

        return words;
    }
}
