using System.Runtime.CompilerServices;
using System.Text.Json;
using OmpOS.Contracts.Catalog;

namespace OmpOS.Api.Services;

/// <summary>
/// The bounded history of the user-level event stream. Events carry increasing identifiers; a client that reconnects with the last identifier it
/// saw gets every later event of its owner. A client whose position is older than the retained history, or from another process, gets one
/// <c>resync</c> event and then the live tail.
/// </summary>
internal sealed class CatalogEventHub
{
    /// <summary>The number of events the hub retains.</summary>
    public const int Capacity = 1024;

    private readonly System.Threading.Lock _gate = new();
    private readonly List<CatalogEventRecord> _history = [];
    private TaskCompletionSource _tick = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _last;
    private int _subscribers;

    /// <summary>Gets the number of connected streams.</summary>
    public int SubscriberCount => Volatile.Read(ref _subscribers);

    /// <summary>Gets the identifier of the newest event, or 0 when none was published.</summary>
    public long LastId
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    /// <summary>Appends an event to the history and wakes the connected streams.</summary>
    /// <param name="ownerId">The owner the event is for.</param>
    /// <param name="kind">The SSE event name.</param>
    /// <param name="json">The JSON data.</param>
    /// <returns>The event identifier.</returns>
    public long Publish(string ownerId, string kind, string json)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(json);
        TaskCompletionSource previous;
        long id;
        lock (_gate)
        {
            id = ++_last;
            _history.Add(new CatalogEventRecord(id, ownerId, kind, json));
            if (_history.Count > Capacity)
            {
                _history.RemoveAt(0);
            }

            previous = _tick;
            _tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        previous.TrySetResult();
        return id;
    }

    /// <summary>Streams the events of one owner.</summary>
    /// <param name="ownerId">The owner whose events are delivered.</param>
    /// <param name="afterId">The last identifier the client saw, or null to start at the live tail.</param>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The events, never ending until cancelled.</returns>
    public async IAsyncEnumerable<CatalogEventRecord> SubscribeAsync(string ownerId, long? afterId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        _ = Interlocked.Increment(ref _subscribers);
        try
        {
            long cursor;
            var resync = false;
            lock (_gate)
            {
                if (afterId is not { } requested)
                {
                    cursor = _last;
                }
                else if (requested > _last)
                {
                    cursor = _last;
                    resync = true;
                }
                else
                {
                    var oldest = _history.Count > 0 ? _history[0].Id : _last + 1;
                    cursor = requested;
                    if (requested + 1 < oldest)
                    {
                        cursor = oldest - 1;
                        resync = true;
                    }
                }
            }

            if (resync)
            {
                yield return new CatalogEventRecord(
                    0,
                    ownerId,
                    CatalogEventKinds.Resync,
                    JsonSerializer.Serialize(new CatalogResyncEvent { Reason = "The requested position is no longer retained. Reload the session list." }, CatalogJsonContext.Default.CatalogResyncEvent));
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                CatalogEventRecord[] batch;
                Task wake;
                lock (_gate)
                {
                    batch = [.. _history.Where(record => record.Id > cursor)];
                    wake = _tick.Task;
                }

                if (batch.Length == 0)
                {
                    if (!await WaitQuietlyAsync(wake, cancellationToken).ConfigureAwait(false))
                    {
                        yield break;
                    }

                    continue;
                }

                cursor = batch[^1].Id;
                foreach (var record in batch)
                {
                    if (string.Equals(record.OwnerId, ownerId, StringComparison.Ordinal))
                    {
                        yield return record;
                    }
                }
            }
        }
        finally
        {
            _ = Interlocked.Decrement(ref _subscribers);
        }
    }

    private static async Task<bool> WaitQuietlyAsync(Task wake, CancellationToken cancellationToken)
    {
        try
        {
            await wake.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
