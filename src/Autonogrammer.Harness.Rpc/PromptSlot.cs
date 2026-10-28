using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>One admitted prompt in flight: its command id and the bounded stream of events its consumer drains.</summary>
internal sealed class PromptSlot
{
    private const int EventCapacity = 4096;

    /// <summary>Creates a slot for the prompt command with <paramref name="id"/>.</summary>
    /// <param name="id">The correlation id of the <c>prompt</c> command.</param>
    public PromptSlot(string id)
    {
        Id = id;
        Events = System.Threading.Channels.Channel.CreateBounded<HarnessEvent>(
            new BoundedChannelOptions(EventCapacity) { SingleReader = true });
    }

    /// <summary>Gets the correlation id of the <c>prompt</c> command.</summary>
    public string Id { get; }

    /// <summary>Gets the events for the consumer, completed when the prompt ends.</summary>
    public Channel<HarnessEvent> Events { get; }

    /// <summary>Gets or sets whether omp admitted the prompt. Only the consumer reads or writes this.</summary>
    public bool Admitted { get; set; }
}
