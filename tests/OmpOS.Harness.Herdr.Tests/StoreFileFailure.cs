namespace OmpOS.Harness.Herdr.Tests;

/// <summary>Captures the failure of an action that must fail because the store path is not a writable or readable file.</summary>
internal static class StoreFileFailure
{
    /// <summary>
    /// Runs the action and returns its failure. Only the two exception types a refused file operation raises are accepted: an
    /// I/O failure and an access refusal. Any other exception propagates and fails the test, and an action that completes
    /// without failing also fails the test.
    /// </summary>
    /// <param name="action">The action that must fail.</param>
    /// <returns>The file-system failure.</returns>
    public static async Task<Exception> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (IOException failure)
        {
            return failure;
        }
        catch (UnauthorizedAccessException failure)
        {
            return failure;
        }

        throw new InvalidOperationException("The action completed without the expected file-system failure.");
    }
}
