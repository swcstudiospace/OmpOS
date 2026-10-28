using System.Text;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>Flattens a session transcript into the plain text the conversation search indexes.</summary>
public static class ConversationSearchTranscriptText
{
    /// <summary>Builds the searchable text of a transcript: the user's prompts, the assistant's text, code, errors and the names of the tools it called, one block per line.</summary>
    /// <param name="transcript">The transcript, with its streaming tail.</param>
    /// <returns>The text, which may be empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transcript"/> is null.</exception>
    public static string From(UiTree transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var builder = new StringBuilder();
        foreach (var block in transcript.Blocks)
        {
            var text = block switch
            {
                UserMessageBlock user => user.Text,
                MarkdownBlock markdown => markdown.Markdown,
                CodeBlock code => code.Code,
                ErrorBlock error => error.Message,
                ToolCallBlock call => call.ToolName,
                _ => string.Empty,
            };
            if (text.Length > 0)
            {
                builder.Append(text).Append('\n');
            }
        }

        builder.Append(transcript.StreamingText);
        return builder.ToString();
    }
}
