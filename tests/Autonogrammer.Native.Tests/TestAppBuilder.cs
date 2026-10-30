using Avalonia;
using Avalonia.Headless;

[assembly: Xunit.v3.ParallelizationAttribute(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The headless application every headless test in this assembly runs on. Isolation is per test (D-20-06),
/// and test parallelism is disabled (A-05, AvaloniaUI/Avalonia#22021).
/// </summary>
/// <remarks>
/// Avalonia's headless host finds <c>BuildAvaloniaApp</c> by reflection with <c>BindingFlags.NonPublic</c> as well as
/// <c>BindingFlags.Public</c> (<c>AppBuilder.Configure(Type)</c>), so this type is internal.
/// </remarks>
internal static class TestAppBuilder
{
    /// <summary>Builds the native application on the headless platform, with its tokens and styles.</summary>
    /// <returns>The application builder the headless runner starts.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    /// <summary>
    /// Starts an isolated headless session on <see cref="TestAppBuilder"/>. Every control touch and dispatcher call of a
    /// test runs inside the session's <c>Dispatch</c> methods. The caller disposes the session with <c>await using</c>:
    /// the synchronous <c>Dispose</c> waits on the dispatcher loop, which the test continuation runs on, and deadlocks.
    /// </summary>
    /// <returns>The started session.</returns>
    public static HeadlessUnitTestSession StartSession() =>
        HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder), AvaloniaTestIsolationLevel.PerTest);

    /// <summary>Runs an async test body on the session's dispatcher thread and completes when the body does.</summary>
    /// <param name="session">The session to run the body in.</param>
    /// <param name="body">The body. Its control touches and awaits continue on the dispatcher thread.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>A task that completes when the body completes, or faults with its exception.</returns>
    public static async Task RunAsync(this HeadlessUnitTestSession session, Func<Task> body, CancellationToken cancellationToken)
    {
        await session.Dispatch<bool>(async () =>
        {
            await body().ConfigureAwait(true);
            return true;
        }, cancellationToken).ConfigureAwait(true);
    }
}
