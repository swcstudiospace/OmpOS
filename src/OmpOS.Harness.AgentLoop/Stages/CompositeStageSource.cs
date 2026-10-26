using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// Merges several stage sources into one ordered stream. Each child runs on its own task and writes into one bounded channel, so
/// a slow consumer slows the children (back-pressure) and no event is dropped. The events of one child keep their order;
/// events of different children interleave in arrival order. A child that throws, or ends before it was asked to, produces one
/// Degraded event titled with the child's name, and the other children keep running. An event whose session, source and stage
/// key repeats the previous event for that key with the same status, title and detail is not emitted again. Cancelling the
/// enumeration, or disposing it early, cancels every child, disposes every child enumerator and waits for every task.
/// </summary>
public sealed class CompositeStageSource : IStageSource
{
    /// <summary>The number of events the merge channel holds before the children wait for the consumer.</summary>
    public const int ChannelCapacity = 256;

    private const int MaxDetailLength = 200;

    private readonly IReadOnlyList<NamedStageSource> _children;
    private readonly TimeProvider _clock;
    private readonly int _capacity;

    /// <summary>Initializes a new instance of the <see cref="CompositeStageSource"/> class.</summary>
    /// <param name="children">The sources to merge, in composition order.</param>
    /// <param name="timeProvider">The clock that stamps the Degraded events.</param>
    public CompositeStageSource(IEnumerable<NamedStageSource> children, TimeProvider timeProvider)
        : this(children, timeProvider, ChannelCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CompositeStageSource"/> class with an explicit channel capacity, so a test can reach back-pressure with few events.</summary>
    /// <param name="children">The sources to merge, in composition order.</param>
    /// <param name="timeProvider">The clock that stamps the Degraded events.</param>
    /// <param name="capacity">The number of events the merge channel holds. At least 1.</param>
    internal CompositeStageSource(IEnumerable<NamedStageSource> children, TimeProvider timeProvider, int capacity)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _children = [.. children];
        foreach (var child in _children)
        {
            if (child is null || string.IsNullOrWhiteSpace(child.Name) || child.Inner is null)
            {
                throw new ArgumentException("Every child needs a name and a source.", nameof(children));
            }
        }

        _clock = timeProvider;
        _capacity = capacity;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(
        StageObservationScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_children.Count == 0)
        {
            yield break;
        }

        var channel = Channel.CreateBounded<StageEvent>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = stop.Token;
        var coordinator = StartPumpsAsync(scope, channel.Writer, token);

        try
        {
            var last = new Dictionary<StageKey, StageSignature>();
            var reader = channel.Reader;
            while (true)
            {
                if (!await WaitToReadAsync(reader, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                while (reader.TryRead(out var stageEvent))
                {
                    var key = new StageKey(stageEvent.SessionId, stageEvent.Source, stageEvent.StageId);
                    var signature = new StageSignature(stageEvent.Status, stageEvent.Title, stageEvent.Detail);
                    if (last.TryGetValue(key, out var previous) && previous == signature)
                    {
                        continue;
                    }

                    last[key] = signature;
                    yield return stageEvent;
                }
            }
        }
        finally
        {
            // Runs on completion, on cancellation and when the consumer stops early: no task or child enumerator outlives the call.
            await stop.CancelAsync().ConfigureAwait(false);
            await coordinator.ConfigureAwait(false);
        }
    }

    private static async Task<bool> WaitToReadAsync(ChannelReader<StageEvent> reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var end = message.AsSpan().IndexOfAny('\r', '\n');
        var line = end >= 0 ? message.AsSpan(0, end) : message.AsSpan();
        var builder = new StringBuilder(Math.Min(line.Length, MaxDetailLength));
        var previousSpace = true;
        foreach (var character in line)
        {
            var space = char.IsControl(character) || char.IsWhiteSpace(character);
            if (space && previousSpace)
            {
                continue;
            }

            builder.Append(space ? ' ' : character);
            previousSpace = space;
            if (builder.Length >= MaxDetailLength)
            {
                break;
            }
        }

        return builder.ToString().TrimEnd();
    }

    private StageEvent Degraded(NamedStageSource child, StageObservationScope scope, string detail) => new()
    {
        SessionId = scope.SessionId,
        Source = child.Source,
        StageId = "source-fault:" + child.Name,
        Title = child.Name,
        Status = StageStatus.Degraded,
        Timestamp = _clock.GetUtcNow(),
        Detail = detail,
        GraphId = scope.GraphId,
    };

    private Task StartPumpsAsync(StageObservationScope scope, ChannelWriter<StageEvent> writer, CancellationToken token)
    {
        var pumps = new Task[_children.Count];
        for (var index = 0; index < pumps.Length; index++)
        {
            var child = _children[index];
            pumps[index] = Task.Run(() => PumpAsync(child, scope, writer, token), CancellationToken.None);
        }

        return Task.Run(
            async () =>
            {
                try
                {
                    await Task.WhenAll(pumps).ConfigureAwait(false);
                }
                finally
                {
                    writer.TryComplete();
                }
            },
            CancellationToken.None);
    }

    private async Task PumpAsync(
        NamedStageSource child,
        StageObservationScope scope,
        ChannelWriter<StageEvent> writer,
        CancellationToken cancellationToken)
    {
        IAsyncEnumerator<StageEvent>? enumerator = null;
        string? fault = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            enumerator = child.Inner.ObserveAsync(scope, cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                await writer.WriteAsync(enumerator.Current, cancellationToken).ConfigureAwait(false);
            }

            if (!child.MayComplete && !cancellationToken.IsCancellationRequested)
            {
                fault = "The stage source ended before it was stopped.";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The enumeration was cancelled: ending quietly is the contract, not a fault.
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // One faulty child must never stop the others; the fault is reported as a Degraded stage.
            var message = Sanitize(ex.Message);
            fault = message.Length == 0 ? ex.GetType().Name : ex.GetType().Name + ": " + message;
        }
        finally
        {
            if (enumerator is not null)
            {
                try
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Disposal was interrupted by the cancellation that is already stopping the run.
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fault ??= ex.GetType().Name + " while the stage source was disposed.";
                }
            }
        }

        if (fault is not null && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await writer.WriteAsync(Degraded(child, scope, fault), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The enumeration was cancelled while the report waited for room; nobody reads it any more.
            }
        }
    }

    private readonly record struct StageKey(string SessionId, StageSource Source, string StageId);

    private readonly record struct StageSignature(StageStatus Status, string Title, string? Detail);
}
