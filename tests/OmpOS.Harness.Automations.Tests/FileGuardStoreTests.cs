using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the file-backed guard store: pauses and run counts survive a reopen, and a damaged file refuses the run
/// instead of admitting it.
/// </summary>
public sealed class FileGuardStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PauseSurvivesReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "state", "guards.json");
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("paused", new ManualTrigger(), Kit.Counting(tally));

        using (var first = new FileAutomationGuardStore(path))
        {
            var engine = Kit.Engine(clock, Kit.ConsentFor("paused"), first, definition);
            engine.SetEnabled("paused", true);
            await engine.PauseAsync(
                "paused",
                new AutomationPause("loop-review", "two self-triggers in one hour", "call ResumeAsync after the review"),
                Token).ConfigureAwait(true);
        }

        using (var second = new FileAutomationGuardStore(path))
        {
            var engine = Kit.Engine(clock, Kit.ConsentFor("paused"), second, definition);
            engine.SetEnabled("paused", true);

            var refused = await engine.RunManualAsync("paused", Token).ConfigureAwait(true);

            refused.Outcome.ShouldBe(AutomationRunOutcome.Refused);
            refused.Guard.ShouldBe("loop-review");
            var pause = refused.Pause.ShouldNotBeNull();
            pause.Reason.ShouldBe("two self-triggers in one hour");
            pause.HowToResume.ShouldBe("call ResumeAsync after the review");
            tally.Count.ShouldBe(0);

            await engine.ResumeAsync("paused", Token).ConfigureAwait(true);
        }

        using var third = new FileAutomationGuardStore(path);
        (await third.FindPauseAsync("paused", Token).ConfigureAwait(true)).ShouldBeNull();
        var resumed = Kit.Engine(clock, Kit.ConsentFor("paused"), third, definition);
        resumed.SetEnabled("paused", true);
        (await resumed.RunManualAsync("paused", Token).ConfigureAwait(true)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunCountsSurviveReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "guards.json");
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define(
            "paced",
            new ManualTrigger(),
            Kit.Counting(tally),
            rateLimit: new RateLimit(2, TimeSpan.FromMinutes(10)));

        using (var first = new FileAutomationGuardStore(path))
        {
            var engine = Kit.Engine(clock, Kit.ConsentFor("paced"), first, definition);
            engine.SetEnabled("paced", true);
            (await engine.RunManualAsync("paced", Token).ConfigureAwait(true)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
            clock.Advance(TimeSpan.FromMinutes(1));
            (await engine.RunManualAsync("paced", Token).ConfigureAwait(true)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        }

        using var second = new FileAutomationGuardStore(path);
        var restarted = Kit.Engine(clock, Kit.ConsentFor("paced"), second, definition);
        restarted.SetEnabled("paced", true);

        // Both runs from before the restart are still inside the window, so the cap holds across the reopen.
        clock.Advance(TimeSpan.FromMinutes(1));
        var overCap = await restarted.RunManualAsync("paced", Token).ConfigureAwait(true);
        overCap.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        overCap.Guard.ShouldBe("rate-limit");
        tally.Count.ShouldBe(2);

        // The first run leaves the window exactly ten minutes after it started; the second is still counted.
        clock.Advance(TimeSpan.FromMinutes(8));
        (await second.CountRunsSinceAsync("paced", clock.GetUtcNow() - TimeSpan.FromMinutes(10), Token).ConfigureAwait(true)).ShouldBe(1);
        (await restarted.RunManualAsync("paced", Token).ConfigureAwait(true)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CorruptFileRefusesRunInsteadOfAdmitting()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "guards.json");
        await File.WriteAllTextAsync(path, "{ \"version\": 1, \"pauses\": ", Token).ConfigureAwait(true);
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("guarded", new ManualTrigger(), Kit.Counting(tally));
        using var store = new FileAutomationGuardStore(path);
        var engine = Kit.Engine(clock, Kit.ConsentFor("guarded"), store, definition);
        engine.SetEnabled("guarded", true);

        var refused = await engine.RunManualAsync("guarded", Token).ConfigureAwait(true);

        refused.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        refused.Guard.ShouldBe("guard-store");
        tally.Count.ShouldBe(0);
        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe("{ \"version\": 1, \"pauses\": ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"version\":2,\"pauses\":{},\"runs\":{}}")]
    [InlineData("{\"version\":1,\"pauses\":{},\"runs\":{},\"extra\":true}")]
    [InlineData("{\"version\":1,\"pauses\":{\"a\":{\"guard\":\" \",\"reason\":\"r\",\"howToResume\":\"h\"}},\"runs\":{}}")]
    public async Task EveryReadOfAnUnusableFileThrowsAndLeavesItUntouched(string content)
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "guards.json");
        await File.WriteAllTextAsync(path, content, Token).ConfigureAwait(true);
        using var store = new FileAutomationGuardStore(path);

        await Should.ThrowAsync<InvalidDataException>(async () => await store.FindPauseAsync("a", Token).ConfigureAwait(true)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidDataException>(async () => await store.CountRunsSinceAsync("a", Kit.Start, Token).ConfigureAwait(true)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidDataException>(async () => await store.RecordRunAsync("a", Kit.Start, Token).ConfigureAwait(true)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe(content);
        Directory.GetFiles(directory.Path).ShouldBe([path]);
    }

    [Fact]
    public async Task UnreadablePathRefusesInsteadOfReadingAsEmpty()
    {
        using var directory = new TempDirectory();

        // A directory where the file should be is not a missing file: the store must not treat it as an empty history.
        using var store = new FileAutomationGuardStore(directory.Path);

        await Should.ThrowAsync<Exception>(async () => await store.CountRunsSinceAsync("a", Kit.Start, Token).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    public async Task MissingFileIsAnEmptyHistoryAndIsNotCreatedByReads()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "guards.json");
        using var store = new FileAutomationGuardStore(path);

        (await store.FindPauseAsync("a", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.CountRunsSinceAsync("a", Kit.Start, Token).ConfigureAwait(true)).ShouldBe(0);
        await store.ResumeAsync("a", Token).ConfigureAwait(true);

        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task RunWindowBoundaryIsExclusiveAndSurvivesReopen()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "guards.json");
        using (var writer = new FileAutomationGuardStore(path))
        {
            await writer.RecordRunAsync("a", Kit.Start, Token).ConfigureAwait(true);
            await writer.RecordRunAsync("a", Kit.Start.AddMinutes(5), Token).ConfigureAwait(true);
            await writer.RecordRunAsync("b", Kit.Start.AddMinutes(6), Token).ConfigureAwait(true);
        }

        using var reader = new FileAutomationGuardStore(path);

        (await reader.CountRunsSinceAsync("a", Kit.Start, Token).ConfigureAwait(true)).ShouldBe(1);
        (await reader.CountRunsSinceAsync("a", Kit.Start.AddMinutes(-1), Token).ConfigureAwait(true)).ShouldBe(2);
        (await reader.CountRunsSinceAsync("b", Kit.Start, Token).ConfigureAwait(true)).ShouldBe(1);
        (await reader.CountRunsSinceAsync("c", Kit.Start, Token).ConfigureAwait(true)).ShouldBe(0);
    }

    [Fact]
    public async Task WriteLeavesOnlyTheStateFileAndItIsOwnerOnly()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nested", "guards.json");
        using var store = new FileAutomationGuardStore(path);

        await store.RecordRunAsync("a", Kit.Start, Token).ConfigureAwait(true);
        await store.PauseAsync("a", new AutomationPause("g", "r", "h"), Token).ConfigureAwait(true);

        Directory.GetFiles(Path.GetDirectoryName(path)!).ShouldBe([path]);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
