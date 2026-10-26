using OmpOS.Contracts.Agents;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.ModelPicker;

/// <summary>The join between the model control of a session and the picker of its composer, and the runtime that creates it for a chat session only.</summary>
public sealed class ModelPickerBindingTests
{
    private static readonly OmpModelChoice[] Models = ModelPickerFixture.Models;

    private static AgentSessionRuntime Runtime(ShellFixture fixture, FakeModelControl? control, Func<CancellationToken, Task>? connect = null) =>
        new(new AgentSessionRuntimeOptions
        {
            Row = fixture.Catalog.Rows[0],
            Prompts = new UnconnectedPromptChannel(),
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = "session-1",
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", "session-1"),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            Models = control,
            ConnectModels = connect,
        });

    /// <summary>A control that already reports models gives a picker that lists them, shows the current one, and maps a confirmed switch to the new model.</summary>
    [Fact]
    public async Task APickerListsWhatTheControlReportsAndSwitchesThroughIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeModelControl();
            control.Report(Models, Models[0].Value);
            using var binding = new ModelPickerBinding(control, connect: null);

            binding.Picker.Choices.Select(static choice => choice.Value).ShouldBe(["anthropic/claude-sonnet", "anthropic/claude-opus", "openai/gpt-x"]);
            binding.Picker.Label.ShouldBe("anthropic / Claude Sonnet");

            await binding.Picker.SelectAsync(binding.Picker.Choices[2]).ConfigureAwait(true);

            control.Requests.ShouldBe(["openai/gpt-x"]);
            binding.Picker.Label.ShouldBe("openai / GPT X");
            binding.Picker.Notice.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A switch the engine refuses becomes a refusal with the engine's message, and the model in use is the one the engine still reports.</summary>
    [Fact]
    public async Task ARefusedSwitchBecomesTheNoticeAndKeepsTheModel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeModelControl { Answer = _ => new OmpModelSwitchResult(OmpModelSwitchOutcome.Busy, "anthropic/claude-sonnet", "The model cannot be changed while a response is running.") };
            control.Report(Models, Models[0].Value);
            using var binding = new ModelPickerBinding(control, connect: null);

            await binding.Picker.SelectAsync(binding.Picker.Choices[1]).ConfigureAwait(true);

            binding.Picker.Notice.ShouldBe("The model cannot be changed while a response is running.");
            binding.Picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A control that reports nothing (no session yet) gives a picker that waits for a load; opening it asks the host to connect, and the report fills the list.</summary>
    [Fact]
    public async Task AnEmptyControlWaitsForTheLoadAndTheReportFillsTheList()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeModelControl();
            var connects = 0;
            using var binding = new ModelPickerBinding(control, _ =>
            {
                connects++;
                control.Report(Models, Models[1].Value);
                return Task.CompletedTask;
            });
            binding.Picker.IsVisible.ShouldBeTrue();
            binding.Picker.Label.ShouldBe(ModelPickerModel.PendingLabel);

            await binding.Picker.EnsureLoadedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            connects.ShouldBe(1);
            binding.Picker.Label.ShouldBe("anthropic / Claude Opus");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When the session behind the control ends, the picker forgets its models and asks the host again the next time it is opened.</summary>
    [Fact]
    public async Task AnEndedSessionResetsThePicker()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeModelControl();
            control.Report(Models, Models[0].Value);
            var connects = 0;
            using var binding = new ModelPickerBinding(control, _ =>
            {
                connects++;
                control.Report(Models, Models[2].Value);
                return Task.CompletedTask;
            });
            binding.Picker.IsLoaded.ShouldBeTrue();

            control.Report([], null);

            binding.Picker.IsLoaded.ShouldBeFalse();
            binding.Picker.Choices.ShouldBeEmpty();
            binding.Picker.Label.ShouldBe(ModelPickerModel.PendingLabel);

            await binding.Picker.EnsureLoadedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

            connects.ShouldBe(1);
            binding.Picker.Label.ShouldBe("openai / GPT X");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A disposed binding no longer follows the control.</summary>
    [Fact]
    public async Task ADisposedBindingStopsFollowingTheControl()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var control = new FakeModelControl();
            control.Report(Models, Models[0].Value);
            var binding = new ModelPickerBinding(control, connect: null);

            binding.Dispose();
            control.Report(Models, Models[2].Value);

            binding.Picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A chat runtime with a model control puts a picker in its composer, and the picker is disabled for as long as the composer streams.</summary>
    [Fact]
    public async Task AChatRuntimeCarriesAPickerThatFollowsTheRun()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-1", "Agent one");
            var control = new FakeModelControl();
            control.Report(Models, Models[0].Value);
            var runtime = Runtime(fixture, control);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            var picker = runtime.Composer.ModelPicker.ShouldNotBeNull();
            picker.IsEnabled.ShouldBeTrue();

            runtime.Composer.IsStreaming = true;
            picker.IsEnabled.ShouldBeFalse();

            runtime.Composer.IsStreaming = false;
            picker.IsEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A runtime with no model control has no picker, and disposing the runtime stops the picker following the control.</summary>
    [Fact]
    public async Task ARuntimeWithoutAControlHasNoPickerAndDisposalReleasesTheControl()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-1", "Agent one");
            var bare = Runtime(fixture, control: null);
            await using (bare.ConfigureAwait(true))
            {
                bare.Composer.ModelPicker.ShouldBeNull();
            }

            var control = new FakeModelControl();
            control.Report(Models, Models[0].Value);
            var runtime = Runtime(fixture, control);
            var picker = runtime.Composer.ModelPicker.ShouldNotBeNull();
            await runtime.DisposeAsync().ConfigureAwait(true);

            control.Report(Models, Models[2].Value);

            picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
