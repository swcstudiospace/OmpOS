using OmpOS.Contracts.Ui;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Commands.Handlers;

namespace OmpOS.Native.Views.Shell;

/// <summary>What the copy commands read from one session: its id, the last response and the last message you sent.</summary>
/// <param name="session">The session view model.</param>
/// <param name="lastRequestId">Gives the id of the last turn's request, or <see langword="null"/> when the engine reported none.</param>
internal sealed class SessionSlashContext(AgentSessionViewModel session, Func<string?> lastRequestId) : ISlashSessionContext
{
    /// <inheritdoc/>
    public string? SessionId => session.Id;

    /// <inheritdoc/>
    public string? LastRequestId => lastRequestId();

    /// <inheritdoc/>
    public string? LastResponseText
    {
        get
        {
            var blocks = session.Session.Tree.Blocks;
            var streaming = session.Session.Tree.StreamingText;
            if (streaming.Length > 0)
            {
                return streaming;
            }

            var last = blocks.OfType<MarkdownBlock>().LastOrDefault();
            return last?.Markdown;
        }
    }

    /// <inheritdoc/>
    public string? LastUserMessageText => session.Session.Tree.Blocks.OfType<UserMessageBlock>().LastOrDefault()?.Text;
}
