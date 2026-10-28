using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>Bounds every wait of a test, so a worker that stops answering fails the test instead of hanging the run.</summary>
internal static class TestTimeouts
{
    /// <summary>Creates a token source that is cancelled when the test run is cancelled or after 60 seconds.</summary>
    /// <returns>The token source. The caller disposes it.</returns>
    public static CancellationTokenSource Create()
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(60));
        return source;
    }
}
