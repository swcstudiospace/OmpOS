using System.Diagnostics;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>
/// Live proof that every row of <see cref="OmpBuiltInProviders"/> is a provider the installed omp lists models for once the provider's own
/// environment variable holds a (fake) key, and that omp lists none without it. omp runs in an isolated home and agent directory with a
/// scrubbed environment and a fake key; no model is called. A cold model cache makes omp refresh its catalogue over the network on its
/// first run, so run it once online. Excluded from the gate by its trait and skipped unless <c>AUTONOGRAMMER_LIVE_OMP=1</c>; run it with
/// <c>AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Native.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class OmpBuiltInProvidersLiveTests
{
    private const string OptInVariable = "AUTONOGRAMMER_LIVE_OMP";
    private const string FakeKey = "fake-key-not-real";

    /// <summary>The variable each provider reads its key from, as <c>omp://providers.md</c> documents it.</summary>
    private static readonly Dictionary<string, string> Variables = new(StringComparer.Ordinal)
    {
        ["openai"] = "OPENAI_API_KEY",
        ["anthropic"] = "ANTHROPIC_API_KEY",
        ["google"] = "GEMINI_API_KEY",
        ["groq"] = "GROQ_API_KEY",
        ["openrouter"] = "OPENROUTER_API_KEY",
        ["mistral"] = "MISTRAL_API_KEY",
        ["xai"] = "XAI_API_KEY",
        ["cerebras"] = "CEREBRAS_API_KEY",
        ["deepseek"] = "DEEPSEEK_API_KEY",
        ["fireworks"] = "FIREWORKS_API_KEY",
        ["together"] = "TOGETHER_API_KEY",
        ["nvidia"] = "NVIDIA_API_KEY",
        ["deepinfra"] = "DEEPINFRA_API_KEY",
        ["huggingface"] = "HF_TOKEN",
        ["moonshot"] = "MOONSHOT_API_KEY",
        ["stepfun"] = "STEPFUN_API_KEY",
        ["novita"] = "NOVITA_API_KEY",
        ["venice"] = "VENICE_API_KEY",
        ["inception"] = "INCEPTION_API_KEY",
        ["baseten"] = "BASETEN_API_KEY",
        ["nanogpt"] = "NANO_GPT_API_KEY",
        ["vercel-ai-gateway"] = "AI_GATEWAY_API_KEY",
        ["zai"] = "ZAI_API_KEY",
        ["minimax"] = "MINIMAX_API_KEY",
    };

    [Fact]
    public async Task EveryProviderInTheTableListsModelsWithItsOwnVariableAndNoneWithout()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to verify the provider table against the installed omp.");
        }

        var location = OmpLocator.Locate(OmpLocatorOptions.FromEnvironment(null));
        if (location is not OmpFound found)
        {
            Assert.Skip("omp is not installed; the live provider table check is skipped.");
            return;
        }

        OmpBuiltInProviders.All.Select(static provider => provider.Id).Order(StringComparer.Ordinal)
            .ShouldBe(Variables.Keys.Order(StringComparer.Ordinal), "the live check covers exactly the rows of the table");
        var cancellationToken = TestContext.Current.CancellationToken;
        var home = Path.Combine(Path.GetTempPath(), "autonogrammer-live-providers-" + Guid.NewGuid().ToString("N"));
        try
        {
            var agentDirectory = Path.Combine(home, ".omp", "agent");
            _ = Directory.CreateDirectory(agentDirectory);
            (await ListAsync(found.ExecutablePath, home, agentDirectory, null, cancellationToken).ConfigureAwait(true)).ShouldNotContain("\"provider\":", Case.Sensitive);
            foreach (var (id, variable) in Variables)
            {
                var output = await ListAsync(found.ExecutablePath, home, agentDirectory, variable, cancellationToken).ConfigureAwait(true);
                output.ShouldContain($"\"provider\":\"{id}\"", Case.Sensitive, id);
            }
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    private static async Task<string> ListAsync(string omp, string home, string agentDirectory, string? variable, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = omp,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("models");
        startInfo.ArgumentList.Add("--json");
        startInfo.ArgumentList.Add("--no-extensions");
        startInfo.Environment.Clear();
        startInfo.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        startInfo.Environment["HOME"] = home;
        startInfo.Environment["PI_CODING_AGENT_DIR"] = agentDirectory;
        if (variable is not null)
        {
            startInfo.Environment[variable] = FakeKey;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("omp did not start.");
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        _ = await errors.ConfigureAwait(false);
        return output;
    }
}
