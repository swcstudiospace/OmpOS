using OmpOS.Presentation.SideChats;

namespace OmpOS.Native.Tests.SideChats;

/// <summary>A side chat engine that answers as scripted, can hold an answer until the test releases it, and records what it was asked, for the view tests.</summary>
internal sealed class ViewSideChatEngine : ISideChatEngine
{
    /// <summary>Gets the requests it received.</summary>
    public List<SideChatRequest> Requests { get; } = [];

    /// <summary>Gets the ids of the chats it was asked to close.</summary>
    public List<string> Closed { get; } = [];

    /// <summary>Gets or sets the answer; null makes the engine fail.</summary>
    public string? Answer { get; set; } = "an answer";

    /// <summary>Gets or sets a gate the next answers wait for, or null to answer at once.</summary>
    public TaskCompletionSource<string>? Gate { get; set; }

    /// <inheritdoc/>
    public async Task<string> AskAsync(SideChatRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (Gate is not null)
        {
            return await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return Answer ?? throw new InvalidOperationException("engine offline");
    }

    /// <inheritdoc/>
    public Task CloseAsync(string chatId, CancellationToken cancellationToken)
    {
        Closed.Add(chatId);
        return Task.CompletedTask;
    }
}
