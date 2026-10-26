using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// The send path of a session that has no engine composed. <see cref="AgentSessionRuntime"/> recognises it and refuses every send with
/// the status line text <see cref="MainView.RefusedSendStatus"/>, so the channel itself is never asked to submit.
/// </summary>
public sealed class UnconnectedPromptChannel : IPromptChannel
{
    /// <summary>The message of the exception a direct submit or cancel throws.</summary>
    public const string UnavailableText = "No engine is connected to this session.";

    /// <inheritdoc/>
    public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(UnavailableText);

    /// <inheritdoc/>
    public Task CancelAsync(CancellationToken cancellationToken) => throw new InvalidOperationException(UnavailableText);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
