using OmpOS.Coordination;

namespace OmpOS.Api.Idempotency;

/// <summary>
/// Records the 202 body of an accepted prompt under its Idempotency-Key. The prompt is already accepted when this runs, so a failure
/// here must never change the response: the completion is retried, and if every attempt fails it is logged and the caller still returns 202.
/// </summary>
internal static class IdempotencyCompletion
{
    /// <summary>Gets the pause before each retry: attempt 2 waits 100 ms, attempt 3 waits 200 ms.</summary>
    private static readonly TimeSpan[] RetryBackoffs = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200)];

    private static readonly Action<ILogger, string, string, Exception?> CompleteFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(40, "IdempotencyCompleteFailed"),
            "Idempotency completion failed for session {SessionId} after three attempts: {ErrorType}. The prompt was accepted; a replay with its key will not return the stored response.");

    /// <summary>Stores the accepted response under the key, retrying up to three attempts in total.</summary>
    /// <param name="store">The idempotency store.</param>
    /// <param name="scope">The scope of the key: the session identifier.</param>
    /// <param name="key">The client's Idempotency-Key.</param>
    /// <param name="fingerprint">The fingerprint of the prompt body.</param>
    /// <param name="responseJson">The serialized 202 body.</param>
    /// <param name="logger">The logger for a completion that failed every attempt.</param>
    /// <returns>A task that completes once the response is stored or every attempt has failed. It never throws.</returns>
    public static async Task CompleteAfterAcceptanceAsync(
        IIdempotencyStore store,
        string scope,
        string key,
        string fingerprint,
        string responseJson,
        ILogger logger)
    {
        var failure = "Unknown";
        for (var attempt = 0; attempt <= RetryBackoffs.Length; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(RetryBackoffs[attempt - 1], CancellationToken.None).ConfigureAwait(false);
            }

            try
            {
                await store.CompleteAsync(scope, key, fingerprint, responseJson, CancellationToken.None).ConfigureAwait(false);
                return;
            }
#pragma warning disable CA1031 // The prompt is already accepted; a failed completion is logged and never turned into a failure response.
            catch (Exception ex)
            {
                failure = ex.GetType().FullName ?? "Unknown";
            }
#pragma warning restore CA1031
        }

        CompleteFailed(logger, scope, failure, null);
    }
}
