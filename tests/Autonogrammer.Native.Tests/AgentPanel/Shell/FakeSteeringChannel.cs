using Autonogrammer.Harness.AgentLoop.Steering;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// A steering channel for tests: it records every directive it was given and every acknowledgement wait, answers each wait with a scripted
/// acknowledgement (delivered unless the test says otherwise), and can refuse a directive the way a session that does not answer does.
/// </summary>
internal sealed class FakeSteeringChannel : ISteeringChannel
{
    private int _count;

    /// <summary>Gets the directives that were queued, in order.</summary>
    public List<SteeringDirective> Sent { get; } = [];

    /// <summary>Gets the identifier and the timeout of every acknowledgement wait, in order.</summary>
    public List<(string Id, TimeSpan Timeout)> Waits { get; } = [];

    /// <summary>Gets or sets the answer to a wait: the acknowledgement of the directive, or null when none arrives in time.</summary>
    public Func<string, SteeringAck?> Acknowledge { get; set; } = static id => new SteeringAck { Id = id, Status = SteeringAckStatus.Delivered };

    /// <summary>Gets or sets the failure the next sends throw, or null when the channel takes the directive.</summary>
    public SteeringUnavailableException? Unavailable { get; set; }

    /// <inheritdoc />
    public Task<SteeringReceipt> SendAsync(SteeringDirective directive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directive);

        if (Unavailable is not null)
        {
            return Task.FromException<SteeringReceipt>(Unavailable);
        }

        _count++;
        Sent.Add(directive);
        return Task.FromResult(new SteeringReceipt($"directive-{_count}", directive.Mode, DateTimeOffset.UnixEpoch));
    }

    /// <inheritdoc />
    public Task<SteeringAck?> WaitForAckAsync(string id, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Waits.Add((id, timeout));
        return Task.FromResult(Acknowledge(id));
    }
}
