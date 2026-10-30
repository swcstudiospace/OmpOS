using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.SlashPalette;

/// <summary>
/// The slash palette end to end: the head's slot composes a real engine host and a real ACP session over a scripted omp peer, and the palette
/// binding of a chat session lists the commands the peer advertises right after <c>session/new</c>, follows a list it advertises later, and runs
/// nothing: a command name typed by hand reaches the peer as an ordinary prompt, exactly as written. No test starts omp or a model.
/// </summary>
public sealed class SlashPaletteEndToEndTests
{
    private static AgentHeadOptions Options(TempTree tree, CommandsOmp omp) => new()
    {
        PluginsRoot = tree.Plugins,
        StateRoot = tree.State,
        WorkspaceRoot = tree.Workspace,
        TransportFactory = omp.TransportFactory,
        SelectMostRecentSession = false,
        MaintenanceInterval = Timeout.InfiniteTimeSpan,
        DetailsRefreshInterval = Timeout.InfiniteTimeSpan,
    };

    /// <summary>
    /// Typing a slash before any prompt starts the session, and the palette lists the real commands omp advertised, tagged omp, next to the
    /// Autonogrammer catalogue, tagged Autonogrammer. The palette sent nothing but the handshake: no prompt, no command.
    /// </summary>
    [Fact]
    public async Task TypingASlashListsTheCommandsOmpAdvertisesAndRunsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new CommandsOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new SlashPaletteBinding(slot.SessionCommands, SlashPaletteBinding.EngineCommandsOnly, slot.Models.ConnectAsync);
            omp.Launches.ShouldBe(0, "no omp child runs before the palette opens");

            binding.Palette.UpdateDraft("/");
            await EventuallyAsync(() => binding.Palette.Entries.Any(static entry => string.Equals(entry.Name, "security", StringComparison.Ordinal)), "the palette lists the commands omp advertised", ct).ConfigureAwait(true);

            var names = binding.Palette.Entries.Select(static entry => entry.Name).ToArray();
            names.ShouldBe(["fast", "model", "security", "skill:review", "switch"]);
            var security = binding.Palette.Entries.Single(static entry => string.Equals(entry.Name, "security", StringComparison.Ordinal));
            security.Source.ShouldBe(SlashPaletteSource.Engine);
            security.Description.ShouldBe("Plan, run, inspect, import, and compare OMP-native security scans");
            security.InputHint.ShouldBe("<plan|scan|status>");
            binding.Palette.Entries.ShouldAllBe(static entry => entry.Source == SlashPaletteSource.Engine, "this binding was built over the engine-only registry, so no catalogue row is listed");
            omp.Launches.ShouldBe(1);
            omp.Requests("session/prompt").ShouldBeEmpty("listing commands never sends a prompt");
            omp.Requests("session/new").Count.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A list omp advertises later (a plugin reload) replaces the one the palette shows.</summary>
    [Fact]
    public async Task ALaterListFromOmpReplacesTheOneThePaletteShows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new CommandsOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new SlashPaletteBinding(slot.SessionCommands, SlashPaletteBinding.EngineCommandsOnly, slot.Models.ConnectAsync);
            binding.Palette.UpdateDraft("/");
            await EventuallyAsync(() => binding.Palette.Entries.Count == 5, "the first list", ct).ConfigureAwait(true);

            omp.Advertise(new OmpCommand("security", "Scans", null), new OmpCommand("plugin:gsd-help", "Show GSD help", null));

            await EventuallyAsync(() => binding.Palette.Entries.Any(static entry => string.Equals(entry.Name, "plugin:gsd-help", StringComparison.Ordinal)), "the replacement list", ct).ConfigureAwait(true);
            binding.Palette.Entries.Select(static entry => entry.Name).ShouldBe(["plugin:gsd-help", "security"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Completing a name only edits the draft. Sending it is the ordinary prompt: a listed command and a name omp never advertised both reach omp as
    /// the text that was written, and omp is the one that decides whether it is a command.
    /// </summary>
    [Fact]
    public async Task CommandsAreRunByOmpWhenThePromptIsSentNotByThePalette()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new CommandsOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new SlashPaletteBinding(slot.SessionCommands, SlashPaletteBinding.EngineCommandsOnly, slot.Models.ConnectAsync);
            binding.Palette.UpdateDraft("/sec");
            await EventuallyAsync(() => binding.Palette.Entries.Any(static entry => string.Equals(entry.Name, "security", StringComparison.Ordinal)), "the palette lists security", ct).ConfigureAwait(true);

            var completed = binding.Palette.Accept();

            completed.ShouldBe("/security ");
            omp.Requests("session/prompt").ShouldBeEmpty("accepting an entry sends nothing");

            await SendAsync(slot, "/security scan", ct).ConfigureAwait(true);
            await SendAsync(slot, "/nonesuch", ct).ConfigureAwait(true);

            omp.Requests("session/prompt").Select(static frame => frame["params"]!["prompt"]![0]!["text"]!.GetValue<string>()).ShouldBe(["/security scan", "/nonesuch"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
