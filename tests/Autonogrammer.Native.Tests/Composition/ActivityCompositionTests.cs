#pragma warning disable VSTHRD200
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Composition.Activity;
using Autonogrammer.Native.Engine;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>The tee keeps event order and forwards the mention-carrying overload.</summary>
public sealed class ActivityCompositionTests
{
    [Fact]
    public async Task TheDecoratorKeepsTheStreamAndForwardsTheMentionOverload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ag-usage-" + Guid.NewGuid().ToString("N"));
        var composition = new ActivityComposition(directory, static () => "session-1");
        var inner = new ScriptChannel(
        [
            new ToolCallEvent("1", "task", "{}"),
            new ContextUsageEvent(3, 10, null),
            new CompletedEvent("end_turn"),
        ]);
        await using var innerLifetime = inner.ConfigureAwait(true);
        var tee = composition.Wrap(inner);
        var seen = new List<HarnessEvent>();
        await foreach (var evt in tee.SubmitAsync("hello", new PromptOptions(), TestContext.Current.CancellationToken).ConfigureAwait(true))
        {
            seen.Add(evt);
        }

        seen.Count.ShouldBe(3);
        seen[0].ShouldBeOfType<ToolCallEvent>();
        seen[2].ShouldBeOfType<CompletedEvent>();
        inner.MentionOverload.ShouldBeTrue();
        composition.UsageHistory.Read().Runs.Count.ShouldBe(1);
    }

    private sealed class ScriptChannel(IReadOnlyList<HarnessEvent> events) : IPromptChannel
    {
        public bool MentionOverload { get; private set; }

        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) => Enumerate();

        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, PromptOptions options, CancellationToken cancellationToken)
        {
            MentionOverload = true;
            return Enumerate();
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async IAsyncEnumerable<HarnessEvent> Enumerate()
        {
            foreach (var evt in events)
            {
                yield return evt;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}

#pragma warning restore VSTHRD200
