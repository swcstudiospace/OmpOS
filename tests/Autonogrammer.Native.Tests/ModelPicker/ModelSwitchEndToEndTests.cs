using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Sessions;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.ModelPicker;

/// <summary>
/// The model picker end to end: the head's slot composes a real engine host and a real ACP session over a scripted omp peer, and the
/// picker binding of a chat session lists the models the peer reports, switches the model through <c>session/set_config_option</c>, follows
/// a change the peer makes on its own, and re-reads the options when the session is closed and reopened. No test starts omp or a model.
/// </summary>
public sealed class ModelSwitchEndToEndTests
{
    private static AgentHeadOptions Options(TempTree tree, ModelOmp omp) => new()
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
    /// Before any prompt the chip reads Model and no omp child runs; opening it starts the session and lists the models; choosing one sends
    /// the exact set_config_option request, omp switches, and the chip shows the new model; a prompt afterwards runs on the same session.
    /// </summary>
    [Fact]
    public async Task SwitchingTheModelOfAChatSessionReachesOmpAndTheChip()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new ModelOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new ModelPickerBinding(slot.Models, slot.Models.ConnectAsync);
            var picker = binding.Picker;

            picker.Label.ShouldBe("Model");
            picker.IsVisible.ShouldBeTrue();
            omp.Launches.ShouldBe(0);

            await picker.EnsureLoadedAsync(ct).ConfigureAwait(true);
            await EventuallyAsync(() => picker.Choices.Count == 3, "the picker lists the models", ct).ConfigureAwait(true);

            picker.Choices.Select(static choice => choice.Value).ShouldBe(["anthropic/claude-sonnet", "anthropic/claude-opus", "openai/gpt-x"]);
            picker.Label.ShouldBe("anthropic / Claude Sonnet");
            omp.Requests("session/new").Count.ShouldBe(1);
            omp.Requests("session/prompt").ShouldBeEmpty();

            await picker.SelectAsync(picker.Choices[1]).ConfigureAwait(true);

            var request = omp.Requests("session/set_config_option").Single();
            request["params"]!["sessionId"]!.GetValue<string>().ShouldBe("native-1");
            request["params"]!["configId"]!.GetValue<string>().ShouldBe("model");
            request["params"]!["value"]!.GetValue<string>().ShouldBe("anthropic/claude-opus");
            omp.Current.ShouldBe("anthropic/claude-opus");
            picker.Label.ShouldBe("anthropic / Claude Opus");
            picker.Notice.ShouldBeNull();

            var turn = await SendAsync(slot, "hi", ct).ConfigureAwait(true);

            turn.Last().ShouldBeOfType<CompletedEvent>();
            omp.Requests("session/new").Count.ShouldBe(1);
            picker.Label.ShouldBe("anthropic / Claude Opus");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A value omp does not offer is refused through the slot without a wire call, and omp keeps the model in use.</summary>
    [Fact]
    public async Task AValueOmpDoesNotOfferIsRefusedWithoutAWireCall()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new ModelOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new ModelPickerBinding(slot.Models, slot.Models.ConnectAsync);
            await binding.Picker.EnsureLoadedAsync(ct).ConfigureAwait(true);

            var result = await slot.Models.SetModelAsync("nobody/nothing", ct).ConfigureAwait(true);

            result.Outcome.ShouldBe(OmpModelSwitchOutcome.UnknownModel);
            omp.Requests("session/set_config_option").ShouldBeEmpty();
            omp.Current.ShouldBe("anthropic/claude-sonnet");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A change omp makes on its own (a slash command) is pushed as config_option_update and moves the chip.</summary>
    [Fact]
    public async Task AModelChangeOmpMakesOnItsOwnMovesTheChip()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new ModelOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new ModelPickerBinding(slot.Models, slot.Models.ConnectAsync);
            await binding.Picker.EnsureLoadedAsync(ct).ConfigureAwait(true);
            await EventuallyAsync(() => binding.Picker.Choices.Count == 3, "the picker lists the models", ct).ConfigureAwait(true);

            omp.ChangeModelOnItsOwn("openai/gpt-x");

            await EventuallyAsync(() => binding.Picker.CurrentValue is "openai/gpt-x", "the chip follows the model omp changed", ct).ConfigureAwait(true);
            binding.Picker.Label.ShouldBe("openai / GPT X");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Closing the session stops its omp and the picker forgets its models; reopening it and opening the chip loads the omp session again
    /// (<c>session/load</c> of the stored session) and re-reads the options, including the model chosen before the close.
    /// </summary>
    [Fact]
    public async Task AReopenedSessionRereadsTheOptions()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new ModelOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            using var binding = new ModelPickerBinding(slot.Models, slot.Models.ConnectAsync);
            var picker = binding.Picker;
            await picker.EnsureLoadedAsync(ct).ConfigureAwait(true);
            await EventuallyAsync(() => picker.Choices.Count == 3, "the picker lists the models", ct).ConfigureAwait(true);
            await picker.SelectAsync(picker.Choices[2]).ConfigureAwait(true);
            picker.CurrentValue.ShouldBe("openai/gpt-x");

            await slot.CloseEngineAsync().ConfigureAwait(true);
            await EventuallyAsync(() => !picker.IsLoaded, "the picker forgets the models of the closed session", ct).ConfigureAwait(true);
            picker.Label.ShouldBe("Model");
            slot.ReopenEngine();
            await picker.EnsureLoadedAsync(ct).ConfigureAwait(true);
            await EventuallyAsync(() => picker.Choices.Count == 3, "the picker lists the models of the reloaded session", ct).ConfigureAwait(true);

            omp.Launches.ShouldBe(2);
            omp.Requests("session/load").Single()["params"]!["sessionId"]!.GetValue<string>().ShouldBe("native-1");
            omp.Requests("session/new").Count.ShouldBe(1);
            picker.CurrentValue.ShouldBe("openai/gpt-x");
            picker.Choices.Count.ShouldBe(3);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A closed session refuses to load its models: the picker shows the reason and stays unloaded, so the next open tries again.</summary>
    [Fact]
    public async Task AClosedSessionCannotLoadItsModels()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            using var tree = new TempTree();
            var omp = new ModelOmp();
            var head = AgentHead.Create(Options(tree, omp));
            await using var headLifetime = head.ConfigureAwait(true);
            await head.InitializeAsync(ct).ConfigureAwait(true);
            var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
            slot.MarkEngineClosed();
            using var binding = new ModelPickerBinding(slot.Models, slot.Models.ConnectAsync);

            await binding.Picker.EnsureLoadedAsync(ct).ConfigureAwait(true);

            binding.Picker.Notice.ShouldBe("The models could not be loaded: This agent is closed. Reopen it to continue.");
            binding.Picker.IsLoaded.ShouldBeFalse();
            omp.Launches.ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
