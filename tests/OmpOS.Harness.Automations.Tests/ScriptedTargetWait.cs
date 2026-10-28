namespace OmpOS.Harness.Automations.Tests;

/// <summary>Waits for a condition without sleeping: it yields to the scheduler until the condition holds or the test is cancelled.</summary>
internal static class ScriptedTargetWait
{
    /// <summary>Yields until <paramref name="condition"/> is true.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="cancellationToken">Cancels the wait, which fails the test.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    public static async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }
}
