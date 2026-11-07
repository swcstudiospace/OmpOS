using System.Runtime.CompilerServices;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Engine;

namespace Autonogrammer.Native.Composition.Activity;

/// <summary>
/// Forwards both <see cref="IPromptChannel.SubmitAsync(string, CancellationToken)"/> and <see cref="IPromptChannel.SubmitAsync(string, PromptOptions, CancellationToken)"/> and every event unchanged, and hands
/// tool, usage, completion, error and permission events to the activity sources. It wraps <c>slot.Prompts</c>
/// where <c>SessionEngineSet.RuntimeOptions</c> assigns Prompts. AgentSessionRuntime is not edited.
/// NotificationRequest and INotificationSink live in Harness.Notifications. Presentation never references them:
/// the bridge sits in Native and Native.Views.
/// </summary>
internal sealed class ActivityTeePromptChannel : IPromptChannel
{
    private readonly IPromptChannel _inner;
    private readonly Action<HarnessEvent> _observe;

    /// <summary>Creates a tee over an existing channel.</summary>
    /// <param name="inner">The channel the session already has.</param>
    /// <param name="observe">Receives the named events. Null ignores them. The stream itself is unchanged.</param>
    public ActivityTeePromptChannel(IPromptChannel inner, Action<HarnessEvent>? observe = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _observe = observe ?? (_ => { });
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
        ForwardAsync(_inner.SubmitAsync(prompt, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, PromptOptions options, CancellationToken cancellationToken) =>
        ForwardAsync(_inner.SubmitAsync(prompt, options, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken) => _inner.CancelAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private async IAsyncEnumerable<HarnessEvent> ForwardAsync(IAsyncEnumerable<HarnessEvent> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (evt is ToolCallEvent or ToolCallUpdateEvent or ContextUsageEvent or CompletedEvent or ErrorEvent or PermissionRequestEvent)
            {
                _observe(evt);
            }

            yield return evt;
        }
    }
}
