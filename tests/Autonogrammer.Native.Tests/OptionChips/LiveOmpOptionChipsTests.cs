using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Composer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.OptionChips;

/// <summary>
/// Live proof that the engine the head composes, connected to the installed omp, fills the mode and thinking chips: the host signs in with
/// omp's <c>agent</c> authentication method, opens a session without a model turn, and omp's own options reach the two chip bindings the
/// session runtime creates (Default and Plan for the mode; omp's thinking levels). omp runs in an isolated home and agent directory with
/// a fake key. Excluded from the gate by its trait and skipped unless <c>AUTONOGRAMMER_LIVE_OMP=1</c>; run it with
/// <c>AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Native.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveOmpOptionChipsTests
{
    private const string OptInVariable = "AUTONOGRAMMER_LIVE_OMP";
    private static readonly TimeSpan OpenBudget = TimeSpan.FromMinutes(5);

    /// <summary>The composed engine opens a session in the real omp and the mode and thinking chips show omp's values.</summary>
    [Fact]
    public async Task TheComposedEngineFillsTheModeAndThinkingChipsFromTheRealOmp()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the chips against the installed omp.");
        }

        if (OmpLocator.Locate(OmpLocatorOptions.FromEnvironment(null)) is not OmpFound found)
        {
            Assert.Skip("omp is not installed; the live chip check is skipped.");
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "autonogrammer-live-chips-" + Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var agentDirectory = Path.Combine(home, ".omp", "agent");
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(agentDirectory);
        Directory.CreateDirectory(workspace);
        try
        {
            var plan = SandboxPlan(home, agentDirectory, workspace);
            var engine = EngineComposition.Compose(["read"], plan, ompPath: found.ExecutablePath);
            await using var hostScope = engine.Host.ConfigureAwait(true);
            await using var session = TestAppBuilder.StartSession();
            await session.RunAsync(async () =>
            {
                OptionChipModel? mode = null;
                OptionChipModel? thinking = null;
                using var modeBinding = new OptionChipBinding(engine.Host.SessionOptions, "mode", "Mode", chip => mode = chip);
                using var thinkingBinding = new OptionChipBinding(engine.Host.SessionOptions, "thinking", "Thinking", chip => thinking = chip);
                mode.ShouldBeNull("no session is open yet");

                using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                budget.CancelAfter(OpenBudget);
                await engine.Host.ConnectAsync(budget.Token).ConfigureAwait(true);
                while (mode is null || thinking is null)
                {
                    await Task.Delay(50, budget.Token).ConfigureAwait(true);
                }

                AssertChips(mode, thinking);
            }, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static OmpLaunchPlan SandboxPlan(string home, string agentDirectory, string workspace) => new()
    {
        ExtraArguments = ["--no-extensions"],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            ["PI_CODING_AGENT_DIR"] = agentDirectory,
            ["OPENAI_API_KEY"] = "fake-key-not-real",
        },
        McpServers = [],
        WorkingDirectory = workspace,
        PlanningEvents = [],
    };

    private static void AssertChips(OptionChipModel mode, OptionChipModel thinking)
    {
        mode.OptionId.ShouldBe("mode");
        mode.Choices.Select(static choice => choice.Value).ShouldBe(["default", "plan"]);
        mode.Choices.Select(static choice => choice.Name).ShouldBe(["Default", "Plan"]);
        mode.Label.ShouldBe("Default");
        mode.ToolTipText.ShouldStartWith("Mode: Default");
        mode.IsEnabled.ShouldBeTrue();

        thinking.OptionId.ShouldBe("thinking");
        thinking.Choices.ShouldNotBeEmpty();
        thinking.Choices.ShouldAllBe(static choice => !string.IsNullOrWhiteSpace(choice.Value) && !string.IsNullOrWhiteSpace(choice.Name));
        thinking.IsEnabled.ShouldBeTrue();
    }
}
