using System.Collections.Concurrent;

namespace OmpOS.Worktrees;

/// <summary>
/// Serializes asynchronous critical sections per string key.
/// </summary>
internal sealed class KeyedAsyncLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Waits until no other holder owns the key, then returns a releaser that frees it.
    /// </summary>
    /// <param name="key">The lock key, typically a normalized repository path.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A releaser; disposing it releases the key.</returns>
    public async Task<Releaser> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        var semaphore = _semaphores.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    /// <summary>
    /// Releases a key held by <see cref="AcquireAsync"/> when disposed.
    /// </summary>
    public sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        internal Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        /// <summary>
        /// Releases the key. Calling it more than once has no further effect.
        /// </summary>
        public void Dispose()
        {
            var semaphore = Interlocked.Exchange(ref _semaphore, null);
            semaphore?.Release();
        }
    }
}
