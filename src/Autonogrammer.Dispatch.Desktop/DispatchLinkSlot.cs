using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Publishes the welcomed link to the connector's worker without letting an older link clear a newer one.</summary>
internal sealed class DispatchLinkSlot
{
    private readonly Lock _gate = new();
    private TaskCompletionSource<Link> _ready = NewSource();
    private Link? _current;
    private long _generation;

    public void Publish(AcpWebSocketTransport transport, CancellationTokenSource cancellation)
    {
        lock (_gate)
        {
            var link = new Link(transport, cancellation, ++_generation);
            _current = link;
            _ready.TrySetResult(link);
        }
    }

    public Link? Current(AcpWebSocketTransport transport)
    {
        lock (_gate)
        {
            return _current is not null && ReferenceEquals(_current.Transport, transport) ? _current : null;
        }
    }

    public void Detach(AcpWebSocketTransport transport)
    {
        lock (_gate)
        {
            if (_current is not null && ReferenceEquals(_current.Transport, transport))
            {
                _current = null;
                _ready = NewSource();
            }
        }
    }

    public void Break(Link link)
    {
        var cancel = false;
        lock (_gate)
        {
            if (ReferenceEquals(_current, link))
            {
                _current = null;
                _ready = NewSource();
                cancel = true;
            }
        }

        if (cancel)
        {
            try
            {
                link.Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The old link finished disposal after it was detached.
            }
        }
    }

    public Task<Link> WaitAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return _ready.Task.WaitAsync(cancellationToken);
        }
    }

    private static TaskCompletionSource<Link> NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed record Link(AcpWebSocketTransport Transport, CancellationTokenSource Cancellation, long Generation);
}
