using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.SlashPalette;

/// <summary>What the binding does with a send: runs a native action through the head's host, expands what discovery found, falls back to omp, and re-reads the conventions when the palette opens (PX-12, PX-13).</summary>
public sealed class SlashPaletteBindingDispatchTests
{
    private static SlashPaletteBinding Bind(
        FakeSessionCommands control,
        ISlashActionHost? host = null,
        ISlashDiscoverySource? discovery = null,
        TimeProvider? time = null) =>
        new(control, SlashCommandCatalogue.Shipped, connect: null, host, discovery, time);

    private static SlashDiscovery Deploy() => new(
        [new SlashDiscoveredCommand("deploy", "Deploys.", "<env>", "Deploy $1 now.", SlashScope.Project, ".cursor/commands/deploy.md")],
        [],
        [],
        []);

    /// <summary>A native command runs through the host with the catalogue's argument and the typed text, and the composer shows the host's sentence and sends nothing.</summary>
    [Fact]
    public async Task ANativeCommandRunsThroughTheHost()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = new RecordingHost(SlashActionIds.SettingsOpen);
            using var binding = Bind(new FakeSessionCommands(), host);

            var result = await binding.DispatchAsync("/rules", TestContext.Current.CancellationToken).ConfigureAwait(true);

            result.Kind.ShouldBe(SlashDispatchKind.Handled);
            result.Text.ShouldBe("Opened.");
            host.Requests.ShouldBe([new SlashActionRequest(SlashActionIds.SettingsOpen, "rules", string.Empty)]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A delegated command and a name nobody knows are sent to omp as typed.</summary>
    [Fact]
    public async Task DelegatedAndUnknownTextIsSentAsTyped()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var binding = Bind(new FakeSessionCommands());
            var token = TestContext.Current.CancellationToken;

            var delegated = await binding.DispatchAsync("/rename Plan B", token).ConfigureAwait(true);
            var unknown = await binding.DispatchAsync("/some-plugin-command x", token).ConfigureAwait(true);
            var plain = await binding.DispatchAsync("just a prompt", token).ConfigureAwait(true);

            delegated.Kind.ShouldBe(SlashDispatchKind.SendPrompt);
            delegated.Text.ShouldBe("/rename Plan B");
            unknown.Text.ShouldBe("/some-plugin-command x");
            plain.Text.ShouldBe("just a prompt");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A native command the window cannot run falls back to omp's command of the same name when omp advertises one, and is refused with the reason when it does not.</summary>
    [Fact]
    public async Task ANativeCommandTheWindowCannotRunFallsBackToTheEngine()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeSessionCommands();
            using var binding = Bind(control);
            var token = TestContext.Current.CancellationToken;

            var refused = await binding.DispatchAsync("/model opus", token).ConfigureAwait(true);
            control.Report(FakeSessionCommands.Typical);
            var fallback = await binding.DispatchAsync("/model opus", token).ConfigureAwait(true);

            refused.Kind.ShouldBe(SlashDispatchKind.Handled);
            refused.Text.ShouldBe(SlashPaletteCopy.ActionNotInThisWindow);
            fallback.Kind.ShouldBe(SlashDispatchKind.SendPrompt);
            fallback.Text.ShouldBe("/model opus");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>After the conventions are read, the palette lists a discovered command and a send expands it.</summary>
    [Fact]
    public async Task ADiscoveredCommandIsListedAndExpanded()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new ScriptedDiscovery(Deploy());
            using var binding = Bind(new FakeSessionCommands(), discovery: source);

            await binding.RefreshDiscoveryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            binding.Palette.UpdateDraft("/dep");
            var result = await binding.DispatchAsync("/deploy staging", TestContext.Current.CancellationToken).ConfigureAwait(true);

            binding.Palette.Entries.ShouldHaveSingleItem().Source.ShouldBe(SlashPaletteSource.Command);
            result.Kind.ShouldBe(SlashDispatchKind.SendPrompt);
            result.Text.ShouldBe("Deploy staging now.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A read that fails keeps what the palette already lists.</summary>
    [Fact]
    public async Task AFailedReadKeepsThePreviousRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new ScriptedDiscovery(Deploy());
            using var binding = Bind(new FakeSessionCommands(), discovery: source);
            await binding.RefreshDiscoveryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            source.Fail = true;
            await binding.RefreshDiscoveryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            source.Loads.ShouldBe(2);
            binding.Palette.UpdateDraft("/dep");
            binding.Palette.Entries.ShouldHaveSingleItem().Name.ShouldBe("deploy");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening the palette reads the conventions, and does not read them again within five seconds; after that an open reads again, so a file added meanwhile appears without a restart.</summary>
    [Fact]
    public async Task OpeningThePaletteRereadsTheConventionsAtMostEveryFiveSeconds()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var clock = new ManualTimeProvider();
            var source = new ScriptedDiscovery(Deploy());
            using var binding = Bind(new FakeSessionCommands(), discovery: source, time: clock);

            binding.Palette.UpdateDraft("/");
            source.Loads.ShouldBe(1);

            binding.Palette.UpdateDraft(string.Empty);
            binding.Palette.UpdateDraft("/");
            source.Loads.ShouldBe(1, "less than five seconds have passed");

            clock.Advance(SlashPaletteBinding.DiscoveryRefreshInterval);
            binding.Palette.UpdateDraft(string.Empty);
            binding.Palette.UpdateDraft("/");
            source.Loads.ShouldBe(2);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A binding with no discovery source reads nothing, and a disposed binding reads nothing.</summary>
    [Fact]
    public async Task NoSourceOrADisposedBindingReadsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var bare = Bind(new FakeSessionCommands());
            await bare.RefreshDiscoveryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            var source = new ScriptedDiscovery(Deploy());
            var disposed = Bind(new FakeSessionCommands(), discovery: source);
            disposed.Dispose();
            await disposed.RefreshDiscoveryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            source.Loads.ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A host given later replaces the first: the native row that was disabled becomes enabled.</summary>
    [Fact]
    public async Task ALaterHostEnablesTheNativeRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var binding = Bind(new FakeSessionCommands());
            binding.Palette.UpdateDraft("/clear");
            binding.Palette.Entries.ShouldHaveSingleItem().IsEnabled.ShouldBeFalse();

            binding.SetActionHost(new RecordingHost(SlashActionIds.ChatNew));

            binding.Palette.Entries.ShouldHaveSingleItem().IsEnabled.ShouldBeTrue();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class RecordingHost(params string[] runnable) : ISlashActionHost
    {
        public List<SlashActionRequest> Requests { get; } = [];

        public bool CanRun(string actionId) => runnable.Contains(actionId, StringComparer.Ordinal);

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(SlashActionResult.Completed("Opened."));
        }
    }

    private sealed class ScriptedDiscovery(SlashDiscovery discovery) : ISlashDiscoverySource
    {
        public bool Fail { get; set; }

        public int Loads { get; private set; }

        public Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken)
        {
            Loads++;
            return Fail ? Task.FromException<SlashDiscovery>(new IOException("unreadable")) : Task.FromResult(discovery);
        }
    }
}
