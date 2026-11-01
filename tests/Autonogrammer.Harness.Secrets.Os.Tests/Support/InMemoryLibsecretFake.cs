namespace Autonogrammer.Harness.Secrets.Os.Tests.Support;

/// <summary>
/// An in-memory stand-in for libsecret: items keyed by name, the same replace/lookup/clear/search behaviour as the Secret Service, and switches
/// that make every call fail the way a locked collection or an absent service does. It records the thread each call ran on.
/// </summary>
internal sealed class InMemoryLibsecretFake : ILibsecretApi
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);
    private readonly List<(Thread Thread, bool IsThreadPool)> _callThreads = [];

    /// <summary>Gets or sets a failure thrown by every call instead of its normal behaviour.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets or sets an action run at the start of each call, for example to advance a clock.</summary>
    public Action? OnCall { get; set; }

    /// <summary>Gets or sets a value that makes each call wait until its token is cancelled, like an unlock prompt nobody answers.</summary>
    public bool BlockUntilCancelled { get; set; }

    /// <summary>Gets the threads the calls ran on, in call order, and whether each was a thread-pool thread while it ran.</summary>
    public IReadOnlyList<(Thread Thread, bool IsThreadPool)> CallThreads
    {
        get
        {
            lock (_gate)
            {
                return [.. _callThreads];
            }
        }
    }

    /// <summary>Gets the stored names.</summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
            {
                return [.. _items.Keys];
            }
        }
    }

    /// <inheritdoc />
    public void Store(string name, SecretString secret, CancellationToken cancellationToken)
    {
        Begin(cancellationToken);
        lock (_gate)
        {
            _items[name] = secret.Reveal();
        }
    }

    /// <inheritdoc />
    public SecretString? Lookup(string name, CancellationToken cancellationToken)
    {
        Begin(cancellationToken);
        lock (_gate)
        {
            return _items.TryGetValue(name, out string? value) ? new SecretString(value) : null;
        }
    }

    /// <inheritdoc />
    public bool Clear(string name, CancellationToken cancellationToken)
    {
        Begin(cancellationToken);
        lock (_gate)
        {
            return _items.Remove(name);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Search(CancellationToken cancellationToken)
    {
        Begin(cancellationToken);
        lock (_gate)
        {
            return [.. _items.Keys];
        }
    }

    private void Begin(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _callThreads.Add((Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread));
        }

        OnCall?.Invoke();
        if (BlockUntilCancelled)
        {
            _ = cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (Failure is not null)
        {
            throw Failure;
        }
    }
}
