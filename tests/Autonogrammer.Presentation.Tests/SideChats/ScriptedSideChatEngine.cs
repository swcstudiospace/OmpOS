using Autonogrammer.Presentation.SideChats;

namespace Autonogrammer.Presentation.Tests.SideChats;

/// <summary>A side chat engine that answers as scripted and records what it was asked.</summary>
internal sealed class ScriptedSideChatEngine : ISideChatEngine
{
    /// <summary>Gets the requests it received.</summary>
    public List<SideChatRequest> Requests { get; } = [];

    /// <summary>Gets the ids of the chats it was asked to close.</summary>
    public List<string> Closed { get; } = [];

    /// <summary>Gets or sets the answer; null makes the engine fail.</summary>
    public string? Answer { get; set; } = "an answer";

    /// <summary>Gets or sets whether closing fails.</summary>
    public bool CloseFails { get; set; }

    /// <inheritdoc/>
    public Task<string> AskAsync(SideChatRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Answer is null ? Task.FromException<string>(new InvalidOperationException("engine offline")) : Task.FromResult(Answer);
    }

    /// <inheritdoc/>
    public Task CloseAsync(string chatId, CancellationToken cancellationToken)
    {
        Closed.Add(chatId);
        return CloseFails ? Task.FromException(new IOException("child gone")) : Task.CompletedTask;
    }
}
