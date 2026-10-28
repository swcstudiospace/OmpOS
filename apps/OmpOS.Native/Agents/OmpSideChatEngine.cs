using System.Text;
using OmpOS.Native.Engine;
using OmpOS.Harness.Abstractions;
using OmpOS.Presentation.Chats;
using OmpOS.Presentation.SideChats;

namespace OmpOS.Native.Agents;

/// <summary>
/// The engine behind side chats: each side chat is a second <c>omp acp</c> session of its own, separate from the session it was opened from. The first question carries the parent summary
/// as hidden context; later questions continue the same conversation. The child can read and answer but never act: it is started with omp's ask-for-everything approval mode and an
/// approval policy that admits no tool kind, so every tool call is denied before anyone is asked.
/// </summary>
internal sealed class OmpSideChatEngine : ISideChatEngine, IAsyncDisposable
{
    private readonly SessionEngineSet _owner;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Chat> _chats = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Creates the engine over the head's engine set.</summary>
    /// <param name="owner">The engine set that composes the child engines.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public OmpSideChatEngine(SessionEngineSet owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public async Task<string> AskAsync(SideChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var chat = Open(request);
        await chat.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var prompt = chat.Introduced ? request.Question : FirstPrompt(request);
            var answer = await EngineAnswer.CollectAsync(chat.Engine.Host.SubmitPromptAsync(prompt, new PromptOptions(), cancellationToken), cancellationToken).ConfigureAwait(false);
            chat.Introduced = true;
            return answer;
        }
        finally
        {
            _ = chat.Gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync(string chatId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chatId);
        cancellationToken.ThrowIfCancellationRequested();
        Chat? chat;
        lock (_gate)
        {
            _ = _chats.Remove(chatId, out chat);
        }

        if (chat is not null)
        {
            await chat.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Ends every side chat's child conversation.</summary>
    /// <returns>A task that completes when every child engine is stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        Chat[] chats;
        lock (_gate)
        {
            _disposed = true;
            chats = [.. _chats.Values];
            _chats.Clear();
        }

        foreach (var chat in chats)
        {
            await chat.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string FirstPrompt(SideChatRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("You answer a side question about a conversation that is going on elsewhere. Answer from the context below and your general knowledge. Do not run commands and do not change files.\n\n");
        builder.Append("Context of the main conversation:\n").Append(request.ParentSummary).Append("\n\n");
        if (request.History.Count > 0)
        {
            builder.Append("This side conversation so far:\n");
            foreach (var message in request.History)
            {
                builder.Append(message.Author).Append(": ").Append(message.Text).Append('\n');
            }

            builder.Append('\n');
        }

        builder.Append("Question: ").Append(request.Question);
        return builder.ToString();
    }

    private Chat Open(SideChatRequest request)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_chats.TryGetValue(request.ChatId, out var known))
            {
                return known;
            }

            var chat = new Chat(_owner.ComposeSideChatEngine(_owner.WorkspaceRootOf(request.ParentSessionId)));
            _chats[request.ChatId] = chat;
            return chat;
        }
    }

    /// <summary>One side chat's child engine and whether it was given the hidden context yet.</summary>
    private sealed class Chat(EngineComposition engine) : IAsyncDisposable
    {
        public EngineComposition Engine { get; } = engine;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public bool Introduced { get; set; }

        public async ValueTask DisposeAsync()
        {
            await Engine.Host.DisposeAsync().ConfigureAwait(false);
            Gate.Dispose();
        }
    }
}
