using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

/// <summary>A failed compensating terminate is logged at Error, and the original persist failure is still the one thrown.</summary>
public sealed class HerdrCompensationLoggingTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HerdrSessionId Alpha = new("alpha");

    [Fact]
    public async Task AFailedCompensatingTerminateIsLoggedAtErrorAndThePersistFailureIsStillThrown()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        // The store file's path is a directory, so the record cannot be written and the server session must be ended.
        Directory.CreateDirectory(path);
        var client = new FakeHerdrSessionClient();
        var logger = new CapturingLogger<HerdrSessionService>();
        using var service = new HerdrSessionService(client, store, new ManualTimeProvider(Start), logger);
        client.FailNextTerminate();

        var failure = await StoreFileFailure.CaptureAsync(async () =>
        {
            await service.CreateAsync(Alpha, "local-omp", NoCancellation).ConfigureAwait(true);
        }).ConfigureAwait(true);

        (failure is IOException or UnauthorizedAccessException).ShouldBeTrue();
        var error = logger.Entries.ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Exception.ShouldBeOfType<HerdrInvalidTransitionException>();
    }
}
