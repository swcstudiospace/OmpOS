using Autonogrammer.UI.Components.Account;

namespace Autonogrammer.UI.Tests.Account;

/// <summary>An in-memory <see cref="IAuthChannel"/>. Two instances can be paired so a post on one arrives on the other, never on itself.</summary>
internal sealed class FakeBroadcastChannel : IAuthChannel
{
    private readonly List<Action<AuthChannelEnvelope>> _handlers = [];
    private FakeBroadcastChannel? _peer;

    /// <summary>Initializes a new instance of the <see cref="FakeBroadcastChannel"/> class.</summary>
    /// <param name="sameOrigin">False refuses every post, the cross-origin case.</param>
    internal FakeBroadcastChannel(bool sameOrigin = true)
    {
        SameOrigin = sameOrigin;
    }

    /// <inheritdoc />
    public bool SameOrigin { get; }

    /// <summary>Gets the envelopes this side posted.</summary>
    internal List<AuthChannelEnvelope> Posted { get; } = [];

    /// <summary>Pairs two tabs. A post on one is delivered to the other.</summary>
    /// <param name="other">The other tab.</param>
    internal void Pair(FakeBroadcastChannel other)
    {
        _peer = other;
        other._peer = this;
    }

    /// <inheritdoc />
    public void Post(AuthChannelEnvelope envelope)
    {
        Posted.Add(envelope);
        if (!SameOrigin)
        {
            return;
        }

        _peer?.Deliver(envelope);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<AuthChannelEnvelope> handler)
    {
        _handlers.Add(handler);
        return new Subscription(this, handler);
    }

    private void Deliver(AuthChannelEnvelope envelope)
    {
        foreach (var handler in _handlers.ToArray())
        {
            handler(envelope);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly FakeBroadcastChannel _channel;
        private readonly Action<AuthChannelEnvelope> _handler;

        internal Subscription(FakeBroadcastChannel channel, Action<AuthChannelEnvelope> handler)
        {
            _channel = channel;
            _handler = handler;
        }

        public void Dispose() => _channel._handlers.Remove(_handler);
    }
}
