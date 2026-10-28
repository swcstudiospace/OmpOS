using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Lists the chats and terminals of the session catalog as mention sources. Chats are the catalog's chat rows (the open session is left out: a chat does not
/// need to mention itself); terminals are its terminal rows. The catalog holds titles only, so the transcript and the terminal output are read through two
/// delegates the head supplies. Side chats are not catalog rows: the head passes them in.
/// </summary>
public sealed class CatalogMentionProvider : IMentionChatProvider, IMentionTerminalProvider
{
    private readonly SessionCatalogModel _catalog;
    private readonly Func<string, int, CancellationToken, Task<MentionContent>> _readTranscript;
    private readonly Func<string, int, CancellationToken, Task<MentionContent>> _readTerminalOutput;
    private readonly Func<IReadOnlyList<MentionChatInfo>>? _sideChats;
    private readonly Func<string?>? _currentSessionId;

    /// <summary>Creates the provider.</summary>
    /// <param name="catalog">The session catalog model of the Agents rail.</param>
    /// <param name="readTranscript">Reads a chat or side chat transcript as text; it throws <see cref="MentionUnavailableException"/> when the chat is gone.</param>
    /// <param name="readTerminalOutput">Reads the recent output of a terminal; it throws <see cref="MentionUnavailableException"/> when the terminal is gone.</param>
    /// <param name="sideChats">Lists the side chats of the open session, or <see langword="null"/> when the head has none.</param>
    /// <param name="currentSessionId">Returns the open session's identifier, which the chat list leaves out, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public CatalogMentionProvider(
        SessionCatalogModel catalog,
        Func<string, int, CancellationToken, Task<MentionContent>> readTranscript,
        Func<string, int, CancellationToken, Task<MentionContent>> readTerminalOutput,
        Func<IReadOnlyList<MentionChatInfo>>? sideChats = null,
        Func<string?>? currentSessionId = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(readTranscript);
        ArgumentNullException.ThrowIfNull(readTerminalOutput);

        _catalog = catalog;
        _readTranscript = readTranscript;
        _readTerminalOutput = readTerminalOutput;
        _sideChats = sideChats;
        _currentSessionId = currentSessionId;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MentionChatInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var current = _currentSessionId?.Invoke();
        var chats = _catalog.Sessions
            .Where(row => !row.IsTerminal && !string.Equals(row.Id, current, StringComparison.Ordinal))
            .Select(static row => new MentionChatInfo(row.Id, row.Title, false, row.Summary.LastActivity))
            .ToList();
        if (_sideChats?.Invoke() is { } side)
        {
            chats.AddRange(side);
        }

        return Task.FromResult<IReadOnlyList<MentionChatInfo>>(chats);
    }

    /// <inheritdoc/>
    public Task<MentionContent> ReadTranscriptAsync(string id, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _readTranscript(id, maxBytes, cancellationToken);
    }

    /// <inheritdoc/>
    Task<IReadOnlyList<MentionTerminalInfo>> IMentionTerminalProvider.ListAsync(CancellationToken cancellationToken)
    {
        var terminals = _catalog.Sessions
            .Where(static row => row.IsTerminal)
            .OrderByDescending(static row => row.Summary.LastActivity)
            .Select(static row => new MentionTerminalInfo(row.Id, row.Title, row.Status is AgentRowStatus.Running))
            .ToList();
        return Task.FromResult<IReadOnlyList<MentionTerminalInfo>>(terminals);
    }

    /// <inheritdoc/>
    public Task<MentionContent> ReadOutputAsync(string id, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _readTerminalOutput(id, maxBytes, cancellationToken);
    }
}
