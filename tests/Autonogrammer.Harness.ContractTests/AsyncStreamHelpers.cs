using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// Consumes <see cref="IAsyncEnumerable{T}"/> sources through explicit enumerators, so every await
/// carries an explicit <c>ConfigureAwait</c> (<c>await foreach</c> and <c>await using</c> cannot).
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Shared by the contract fixtures and by the InMemory test project that derives them.")]
public static class AsyncStreamHelpers
{
    /// <summary>
    /// Appends every item of <paramref name="source"/> to <paramref name="sink"/>. Items appended before an
    /// error is thrown remain in <paramref name="sink"/>, so tests can assert partial delivery.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The stream to consume.</param>
    /// <param name="sink">Receives each item in order.</param>
    /// <param name="cancellationToken">Cancels the consumer.</param>
    public static async Task DrainAsync<T>(IAsyncEnumerable<T> source, ICollection<T> sink, CancellationToken cancellationToken)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                sink.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Returns the first item of <paramref name="source"/> and disposes the enumerator.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The stream to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The first item.</returns>
    /// <exception cref="InvalidOperationException">The stream completed without yielding an item.</exception>
    public static async Task<T> FirstItemAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            if (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return enumerator.Current;
            }

            throw new InvalidOperationException("The stream completed before yielding an item.");
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}
