using Shouldly;
using Xunit;

namespace OmpOS.Harness.Herdr.Tests;

/// <summary>
/// Every lifecycle transition the service confirms is stored before the call returns, so the last-known status survives a
/// process restart at each step (ROADMAP Phase 37, criterion 1).
/// </summary>
public sealed class HerdrLifecyclePersistenceTests
{
    private static readonly CancellationToken NoCancellation = CancellationToken.None;
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HerdrSessionId Alpha = new("alpha");

    [Fact]
    public async Task PauseResumeAndTerminatePersistTheLastKnownStatusAcrossReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "herdr-sessions.json");
        var client = new FakeHerdrSessionClient();
        var clock = new ManualTimeProvider(Start);

        await UseServiceAsync(path, client, clock, service => service.CreateAsync(Alpha, "local-omp", NoCancellation)).ConfigureAwait(true);
        (await StoredStateAsync(path).ConfigureAwait(true)).ShouldBe(HerdrSessionState.Running);

        await UseServiceAsync(path, client, clock, service => service.PauseAsync(Alpha, NoCancellation)).ConfigureAwait(true);
        (await StoredStateAsync(path).ConfigureAwait(true)).ShouldBe(HerdrSessionState.Paused);

        await UseServiceAsync(path, client, clock, service => service.ResumeAsync(Alpha, NoCancellation)).ConfigureAwait(true);
        (await StoredStateAsync(path).ConfigureAwait(true)).ShouldBe(HerdrSessionState.Running);

        await UseServiceAsync(path, client, clock, service => service.TerminateAsync(Alpha, NoCancellation)).ConfigureAwait(true);
        (await StoredStateAsync(path).ConfigureAwait(true)).ShouldBe(HerdrSessionState.Terminated);
    }

    /// <summary>Runs one service step against a freshly opened store, then closes both, so every step is a separate process lifetime.</summary>
    private static async Task UseServiceAsync(string path, IHerdrSessionClient client, TimeProvider clock, Func<HerdrSessionService, Task> step)
    {
        using var store = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        using var service = new HerdrSessionService(client, store, clock);
        await step(service).ConfigureAwait(true);
    }

    private static async Task<HerdrSessionState> StoredStateAsync(string path)
    {
        using var reopened = await HerdrSessionStore.OpenAsync(path, NoCancellation).ConfigureAwait(true);
        return reopened.Find(Alpha)!.LastKnownState;
    }
}
