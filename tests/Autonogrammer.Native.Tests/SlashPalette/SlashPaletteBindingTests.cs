using Avalonia.Threading;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.SlashPalette;

/// <summary>The binding of the engine's commands to a composer palette: the palette lists what omp advertises, follows a later list, starts the engine session when it opens empty, and stops with the runtime.</summary>
public sealed class SlashPaletteBindingTests
{
    private static SlashCommandRegistry Catalogue() => new(
    [
        new SlashCommandDescriptor
        {
            Name = "side",
            Description = "Opens a side chat.",
            Platforms = CommandPlatforms.Linux,
            HandlerId = "test.side",
        },
    ]);

    /// <summary>The palette lists what the control already reports, next to the catalogue, and follows a list omp advertises later.</summary>
    [Fact]
    public async Task ThePaletteListsWhatOmpAdvertisesAndFollowsALaterList()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var control = new FakeSessionCommands();
            control.Report(FakeSessionCommands.Typical);
            using var binding = new SlashPaletteBinding(control, Catalogue(), connect: null);
            binding.Palette.UpdateDraft("/");

            binding.Palette.Entries.Select(static entry => entry.Name).ShouldBe(["fast", "model", "security", "side", "skill:review"]);
            binding.Palette.Entries.Single(static entry => string.Equals(entry.Name, "security", StringComparison.Ordinal)).InputHint.ShouldBe("<plan|scan|status>");

            control.Report(new OmpCommand("slow", "Toggle slow mode", "[on|off|status]"));

            binding.Palette.Entries.Select(static entry => entry.Name).ShouldBe(["side", "slow"]);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A list advertised from another thread reaches the palette on the UI thread.</summary>
    [Fact]
    public async Task AListFromAnotherThreadReachesThePaletteOnTheUiThread()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            using var binding = new SlashPaletteBinding(control, Catalogue(), connect: null);
            binding.Palette.UpdateDraft("/");
            binding.Palette.Entries.Select(static entry => entry.Name).ShouldBe(["side"]);

            await Task.Run(() => control.Report(FakeSessionCommands.Typical), TestContext.Current.CancellationToken).ConfigureAwait(true);
            Dispatcher.UIThread.RunJobs();

            binding.Palette.Entries.Count.ShouldBe(5);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening the palette before omp advertised anything starts the engine session once; it does not start it when omp already advertised commands, and it does not run twice while it starts.</summary>
    [Fact]
    public async Task OpeningWithNoEngineCommandsStartsTheEngineSessionOnce()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            var connects = 0;
            using var release = new SemaphoreSlim(0, 1);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var binding = new SlashPaletteBinding(
                control,
                Catalogue(),
                async _ =>
                {
                    connects++;
                    await release.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                    control.Report(FakeSessionCommands.Typical);
                    finished.SetResult();
                });

            binding.Palette.UpdateDraft("hello");
            connects.ShouldBe(0, "a closed palette starts nothing");

            binding.Palette.UpdateDraft("/");
            binding.Palette.UpdateDraft("/s");
            binding.Palette.UpdateDraft("/");
            connects.ShouldBe(1, "one start while the first is still running");

            release.Release();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            binding.Palette.Entries.Count.ShouldBe(5);

            binding.Palette.UpdateDraft(string.Empty);
            binding.Palette.UpdateDraft("/");
            connects.ShouldBe(1, "omp advertised its commands, so nothing more to start");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A start that fails is swallowed (the palette keeps the catalogue), and the next opening tries again.</summary>
    [Fact]
    public async Task AFailedStartIsRetriedTheNextTimeThePaletteOpens()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var connects = 0;
            using var binding = new SlashPaletteBinding(
                new FakeSessionCommands(),
                Catalogue(),
                _ =>
                {
                    connects++;
                    return Task.FromException(new InvalidOperationException("omp could not start."));
                });

            // The failed start is already a faulted task, so it is swallowed and its guard is released before UpdateDraft returns.
            binding.Palette.UpdateDraft("/");
            connects.ShouldBe(1);
            binding.Palette.Entries.Select(static entry => entry.Name).ShouldBe(["side"]);

            binding.Palette.UpdateDraft(string.Empty);
            binding.Palette.UpdateDraft("/");
            connects.ShouldBe(2);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing stops the binding from following omp and cancels a start that is still running.</summary>
    [Fact]
    public async Task DisposingStopsFollowingAndCancelsAPendingStart()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var control = new FakeSessionCommands();
            CancellationToken seen = default;
            using var binding = new SlashPaletteBinding(
                control,
                Catalogue(),
                token =>
                {
                    seen = token;
                    return Task.Delay(Timeout.InfiniteTimeSpan, token);
                });
            binding.Palette.UpdateDraft("/");
            seen.CanBeCanceled.ShouldBeTrue();
            seen.IsCancellationRequested.ShouldBeFalse();

            binding.Dispose();
            control.Report(FakeSessionCommands.Typical);

            seen.IsCancellationRequested.ShouldBeTrue();
            binding.Palette.Entries.Select(static entry => entry.Name).ShouldBe(["side"]);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A null control or registry is refused.</summary>
    [Fact]
    public async Task NullArgumentsAreRefused()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            Should.Throw<ArgumentNullException>(() =>
            {
                using var refused = new SlashPaletteBinding(null!, Catalogue(), connect: null);
            });
            Should.Throw<ArgumentNullException>(() =>
            {
                using var refused = new SlashPaletteBinding(new FakeSessionCommands(), null!, connect: null);
            });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
