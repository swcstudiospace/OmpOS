using System.Runtime.CompilerServices;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>
/// A session whose prompt streams one text delta and then fails mid-turn. It proves that a stream fault ends the adapter's
/// reader with a terminal error rather than leaving the reader waiting.
/// </summary>
internal sealed class FaultingSession : IOmpSession
{
    private static readonly IReadOnlyDictionary<string, string> NoCapabilities = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new TextDeltaEvent("partial");
        throw new InvalidOperationException("The engine stream failed mid-turn.");
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public HarnessState State => HarnessState.Ready;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities => NoCapabilities;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
